#!/usr/bin/env bash

set -euo pipefail

root_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
artifacts_dir="${ARTIFACTS_DIR:-${root_dir}/.artifacts}"

rm -rf "${artifacts_dir}"
mkdir -p "${artifacts_dir}"

dotnet publish "${root_dir}/Quiz.ApiGateway/Quiz.ApiGateway.csproj" -c Release -o "${artifacts_dir}/apigateway"
dotnet publish "${root_dir}/Quiz.AuthService/Quiz.AuthService.csproj" -c Release -o "${artifacts_dir}/authservice"
dotnet publish "${root_dir}/Quiz.QuizService/Quiz.QuizService.csproj" -c Release -o "${artifacts_dir}/quizservice"
dotnet publish "${root_dir}/Quiz.LiveSessionService/Quiz.LiveSessionService.csproj" -c Release -o "${artifacts_dir}/livesessionservice"
dotnet publish "${root_dir}/Quiz.CodingService/Quiz.CodingService.csproj" -c Release -o "${artifacts_dir}/codingservice"

pushd "${root_dir}/Quiz.Web/app" >/dev/null
npm ci
npm run build
popd >/dev/null

mkdir -p "${artifacts_dir}/web"
cp -R "${root_dir}/Quiz.Web/app/dist/." "${artifacts_dir}/web/"
