FROM mcr.microsoft.com/dotnet/aspnet:10.0.3 AS final
WORKDIR /app

RUN apt-get update \
    && apt-get install -y --no-install-recommends python3 nodejs npm \
    && rm -rf /var/lib/apt/lists/*

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

COPY .artifacts/codingservice/ ./

ENTRYPOINT ["dotnet", "Quiz.CodingService.dll"]
