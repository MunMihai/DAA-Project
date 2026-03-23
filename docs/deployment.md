# DigitalOcean CI/CD and Kubernetes Deployment

## Architecture

- `web` is the only public service. It runs the Vite build behind Nginx and proxies `/api`, `/hubs`, and `/coding-hubs` to the internal `apigateway` service.
- `apigateway`, `authservice`, `quizservice`, `livesessionservice`, and `codingservice` run as internal `ClusterIP` services.
- `mongo`, `redis`, and `rabbitmq` are deployed inside the cluster with persistent volumes.
- Production secrets are injected via the `quiz-secrets` Kubernetes secret and are not stored in git.

## Required GitHub Secrets

- `DIGITALOCEAN_ACCESS_TOKEN`
- `JWT_SIGNING_KEY`
- `GROQ_API_KEY`
- `MONGO_ROOT_PASSWORD`
- `REDIS_PASSWORD`
- `RABBITMQ_PASSWORD`
- `RABBITMQ_ERLANG_COOKIE`
- `SEED_API_TOKEN`

Optional:

- `MONGO_ROOT_USERNAME` defaults to `quiz_root`
- `RABBITMQ_USERNAME` defaults to `quiz_app`

The DigitalOcean token used by `CD` must be allowed to access both:

- `Kubernetes`
- `Container Registry`

## Workflows

- `CI` runs on macOS and builds the deployable `.NET` publish outputs plus the frontend dist into `.artifacts`.
- `CD` has a macOS packaging job that uploads `.artifacts`, then an Ubuntu deploy job that builds container images from those artifacts, pushes them to DigitalOcean Container Registry `dev-docker-registry`, applies Kubernetes manifests, refreshes the `quiz-secrets` secret, and rolls out the new image tag to the DOKS cluster `k8s-1-35-1-do-0-fra1-1774185110679`.
- `CD` can also run the seed job after a manual `workflow_dispatch` deployment by setting the `run_seed` input to `true`.
- `scripts/deploy/run-seed.sh` creates a short-lived Kubernetes `Job` inside the cluster that calls the internal seed endpoints for `authservice` and `codingservice` using the secret `SEED_API_TOKEN`. The seed UI remains hidden in production.

## Local Deployment

Run the full deployment locally with:

```bash
export DIGITALOCEAN_ACCESS_TOKEN=...
export JWT_SIGNING_KEY=...
export GROQ_API_KEY=...
export MONGO_ROOT_PASSWORD=...
export REDIS_PASSWORD=...
export RABBITMQ_PASSWORD=...
export RABBITMQ_ERLANG_COOKIE=...
export SEED_API_TOKEN=...
bash ./scripts/deploy/doks-deploy.sh
```

To deploy and immediately run the seed job:

```bash
export RUN_SEED_AFTER_DEPLOY=1
bash ./scripts/deploy/doks-deploy.sh
```

To run the seed job later against the current cluster:

```bash
bash ./scripts/deploy/run-seed.sh
```

Optional overrides:

```bash
export MONGO_ROOT_USERNAME=quiz_root
export RABBITMQ_USERNAME=quiz_app
export DOKS_CLUSTER_NAME=k8s-1-35-1-do-0-fra1-1774185110679
export DOCR_REGISTRY_NAME=dev-docker-registry
export DOCR_REGION=fra1
export DOCR_SUBSCRIPTION_TIER=basic
export DOCKER_PLATFORM=linux/amd64
export K8S_NAMESPACE=quiz-platform
export IMAGE_TAG=manual-$(date +%Y%m%d%H%M%S)
```
