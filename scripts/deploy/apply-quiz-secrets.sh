#!/usr/bin/env bash

set -euo pipefail

namespace="${K8S_NAMESPACE:-quiz-platform}"

required_vars=(
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

mongo_root_username="${MONGO_ROOT_USERNAME:-quiz_root}"
rabbitmq_username="${RABBITMQ_USERNAME:-quiz_app}"

mongodb_auth_uri="mongodb://${mongo_root_username}:${MONGO_ROOT_PASSWORD}@mongo:27017/quizauthdb?authSource=admin"
mongodb_quiz_uri="mongodb://${mongo_root_username}:${MONGO_ROOT_PASSWORD}@mongo:27017/quizdb?authSource=admin"
mongodb_coding_uri="mongodb://${mongo_root_username}:${MONGO_ROOT_PASSWORD}@mongo:27017/codingdb?authSource=admin"
redis_connection_string="redis:6379,password=${REDIS_PASSWORD},abortConnect=false"

kubectl -n "${namespace}" create secret generic quiz-secrets \
  --from-literal=mongo-root-username="${mongo_root_username}" \
  --from-literal=mongo-root-password="${MONGO_ROOT_PASSWORD}" \
  --from-literal=mongodb-auth-uri="${mongodb_auth_uri}" \
  --from-literal=mongodb-quiz-uri="${mongodb_quiz_uri}" \
  --from-literal=mongodb-coding-uri="${mongodb_coding_uri}" \
  --from-literal=redis-password="${REDIS_PASSWORD}" \
  --from-literal=redis-connection-string="${redis_connection_string}" \
  --from-literal=rabbitmq-username="${rabbitmq_username}" \
  --from-literal=rabbitmq-password="${RABBITMQ_PASSWORD}" \
  --from-literal=rabbitmq-erlang-cookie="${RABBITMQ_ERLANG_COOKIE}" \
  --from-literal=jwt-signing-key="${JWT_SIGNING_KEY}" \
  --from-literal=groq-api-key="${GROQ_API_KEY}" \
  --from-literal=seed-api-token="${SEED_API_TOKEN}" \
  --dry-run=client \
  -o yaml | kubectl apply -f -
