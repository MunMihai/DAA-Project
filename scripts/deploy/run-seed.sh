#!/usr/bin/env bash

set -euo pipefail

namespace="${K8S_NAMESPACE:-quiz-platform}"
job_name="${SEED_JOB_NAME:-quiz-seed-$(date +%Y%m%d%H%M%S)}"
job_image="${SEED_JOB_IMAGE:-curlimages/curl:8.12.1}"
wait_timeout="${SEED_WAIT_TIMEOUT:-5m}"
keep_job="${KEEP_SEED_JOB:-0}"

if ! command -v kubectl >/dev/null 2>&1; then
  echo "kubectl is required but was not found in PATH." >&2
  exit 1
fi

if ! kubectl -n "${namespace}" get secret quiz-secrets >/dev/null 2>&1; then
  echo "Secret quiz-secrets was not found in namespace ${namespace}." >&2
  exit 1
fi

manifest_file="$(mktemp)"
trap 'rm -f "${manifest_file}"' EXIT

cat >"${manifest_file}" <<EOF
apiVersion: batch/v1
kind: Job
metadata:
  name: ${job_name}
  namespace: ${namespace}
spec:
  backoffLimit: 0
  ttlSecondsAfterFinished: 600
  template:
    metadata:
      labels:
        app.kubernetes.io/name: quiz-seed
    spec:
      restartPolicy: Never
      containers:
        - name: seed
          image: ${job_image}
          command:
            - sh
            - -ceu
            - |
              auth_response=\$(curl --fail --silent --show-error \\
                -X POST \\
                -H "X-Seed-Token: \$SEED_API_TOKEN" \\
                http://authservice:8080/api/auth/run-seed)
              echo "Auth seed response:"
              echo "\$auth_response"

              coding_response=\$(curl --fail --silent --show-error \\
                -X POST \\
                -H "X-Seed-Token: \$SEED_API_TOKEN" \\
                http://codingservice:8080/api/compile-coding-templates/run-seed)
              echo "Coding seed response:"
              echo "\$coding_response"
          env:
            - name: SEED_API_TOKEN
              valueFrom:
                secretKeyRef:
                  name: quiz-secrets
                  key: seed-api-token
EOF

kubectl apply -f "${manifest_file}"

if ! kubectl -n "${namespace}" wait --for=condition=complete --timeout="${wait_timeout}" "job/${job_name}"; then
  kubectl -n "${namespace}" logs "job/${job_name}" --all-containers=true || true
  kubectl -n "${namespace}" describe "job/${job_name}" || true
  exit 1
fi

kubectl -n "${namespace}" logs "job/${job_name}" --all-containers=true

if [[ "${keep_job}" != "1" ]]; then
  kubectl -n "${namespace}" delete job "${job_name}" --ignore-not-found
fi
