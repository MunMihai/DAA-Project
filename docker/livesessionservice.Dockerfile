FROM mcr.microsoft.com/dotnet/aspnet:10.0.3 AS final
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

COPY --link .artifacts/livesessionservice/ ./

ENTRYPOINT ["dotnet", "Quiz.LiveSessionService.dll"]
