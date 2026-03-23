#!/usr/bin/env bash

set -euo pipefail

root_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"

required_vars=(
  DIGITALOCEAN_ACCESS_TOKEN
  JWT_SIGNING_KEY
  GROQ_API_KEY
  MONGO_ROOT_PASSWORD
  REDIS_PASSWORD
  RABBITMQ_PASSWORD
  RABBITMQ_ERLANG_COOKIE
  SEED_API_TOKEN
)

for var_name in "${required_vars[@]}"; do
  if [[ -z "${!var_name:-}" ]]; then
    echo "Missing required environment variable: ${var_name}" >&2
    exit 1
  fi
done

k8s_namespace="${K8S_NAMESPACE:-quiz-platform}"
doks_cluster_name="${DOKS_CLUSTER_NAME:-k8s-1-35-1-do-0-fra1-1774185110679}"
docr_registry_name="${DOCR_REGISTRY_NAME:-dev-docker-registry}"
registry_secret_name="${DOCR_K8S_SECRET_NAME:-registry-${docr_registry_name}}"
docker_registry_region="${DOCR_REGION:-fra1}"
docker_registry_tier="${DOCR_SUBSCRIPTION_TIER:-basic}"
docker_platform="${DOCKER_PLATFORM:-linux/amd64}"
image_tag="${IMAGE_TAG:-$(git -C "${root_dir}" rev-parse --short HEAD)}"
skip_local_build="${SKIP_LOCAL_BUILD:-0}"
run_seed_after_deploy="${RUN_SEED_AFTER_DEPLOY:-0}"

if ! command -v doctl >/dev/null 2>&1; then
  echo "doctl is required but was not found in PATH." >&2
  exit 1
fi

if ! command -v docker >/dev/null 2>&1; then
  echo "docker is required but was not found in PATH." >&2
  exit 1
fi

if ! command -v kubectl >/dev/null 2>&1; then
  echo "kubectl is required but was not found in PATH." >&2
  exit 1
fi

if [[ "${skip_local_build}" != "1" ]]; then
  bash "${root_dir}/scripts/build-artifacts.sh"
fi

doctl auth init -t "${DIGITALOCEAN_ACCESS_TOKEN}"

registry_get_output=""
if ! registry_get_output="$(doctl registry get "${docr_registry_name}" 2>&1)"; then
  if grep -qi "not authorized" <<<"${registry_get_output}"; then
    echo "DigitalOcean token cannot access Container Registry '${docr_registry_name}'." >&2
    echo "Update DIGITALOCEAN_ACCESS_TOKEN with a token from the DigitalOcean account/team that owns the registry and cluster." >&2
    echo "The token must be allowed to use both Kubernetes and Container Registry." >&2
    exit 1
  fi

  if grep -qi "404" <<<"${registry_get_output}"; then
    doctl registry create "${docr_registry_name}" \
      --region "${docker_registry_region}" \
      --subscription-tier "${docker_registry_tier}"
  else
    echo "${registry_get_output}" >&2
    exit 1
  fi
fi

doctl registry login --expiry-seconds 1800

images=(
  "apigateway docker/api-gateway.Dockerfile"
  "authservice docker/authservice.Dockerfile"
  "quizservice docker/quizservice.Dockerfile"
  "livesessionservice docker/livesessionservice.Dockerfile"
  "codingservice docker/codingservice.Dockerfile"
  "web docker/web.Dockerfile"
)

for entry in "${images[@]}"; do
  image_name="${entry%% *}"
  dockerfile="${entry##* }"
  full_image="registry.digitalocean.com/${docr_registry_name}/${image_name}:${image_tag}"

  echo "Building ${full_image}"
  docker buildx build \
    --platform "${docker_platform}" \
    -f "${root_dir}/${dockerfile}" \
    -t "${full_image}" \
    "${root_dir}" \
    --push
done

doctl kubernetes cluster kubeconfig save "${doks_cluster_name}"

kubectl apply -f "${root_dir}/k8s/base/namespace.yaml"
doctl registry kubernetes-manifest "${docr_registry_name}" --namespace "${k8s_namespace}" | kubectl apply -f -
kubectl -n "${k8s_namespace}" patch serviceaccount default \
  --type merge \
  -p "{\"imagePullSecrets\":[{\"name\":\"${registry_secret_name}\"}]}"
bash "${root_dir}/scripts/deploy/apply-quiz-secrets.sh"
kubectl apply -k "${root_dir}/k8s/overlays/digitalocean"

for deployment in authservice quizservice livesessionservice codingservice apigateway web; do
  kubectl -n "${k8s_namespace}" patch deployment "${deployment}" \
    --type strategic \
    -p "{\"spec\":{\"template\":{\"spec\":{\"imagePullSecrets\":[{\"name\":\"${registry_secret_name}\"}]}}}}"
done

kubectl -n "${k8s_namespace}" set image deployment/authservice \
  authservice="registry.digitalocean.com/${docr_registry_name}/authservice:${image_tag}"
kubectl -n "${k8s_namespace}" set image deployment/quizservice \
  quizservice="registry.digitalocean.com/${docr_registry_name}/quizservice:${image_tag}"
kubectl -n "${k8s_namespace}" set image deployment/livesessionservice \
  livesessionservice="registry.digitalocean.com/${docr_registry_name}/livesessionservice:${image_tag}"
kubectl -n "${k8s_namespace}" set image deployment/codingservice \
  codingservice="registry.digitalocean.com/${docr_registry_name}/codingservice:${image_tag}"
kubectl -n "${k8s_namespace}" set image deployment/apigateway \
  apigateway="registry.digitalocean.com/${docr_registry_name}/apigateway:${image_tag}"
kubectl -n "${k8s_namespace}" set image deployment/web \
  web="registry.digitalocean.com/${docr_registry_name}/web:${image_tag}"

kubectl -n "${k8s_namespace}" rollout status deployment/mongo --timeout=10m
kubectl -n "${k8s_namespace}" rollout status deployment/redis --timeout=10m
kubectl -n "${k8s_namespace}" rollout status deployment/rabbitmq --timeout=10m
kubectl -n "${k8s_namespace}" rollout status deployment/authservice --timeout=10m
kubectl -n "${k8s_namespace}" rollout status deployment/quizservice --timeout=10m
kubectl -n "${k8s_namespace}" rollout status deployment/livesessionservice --timeout=10m
kubectl -n "${k8s_namespace}" rollout status deployment/codingservice --timeout=10m
kubectl -n "${k8s_namespace}" rollout status deployment/apigateway --timeout=10m
kubectl -n "${k8s_namespace}" rollout status deployment/web --timeout=10m

if [[ "${run_seed_after_deploy}" == "1" ]]; then
  bash "${root_dir}/scripts/deploy/run-seed.sh"
fi

echo "Current web service:"
kubectl -n "${k8s_namespace}" get service web
