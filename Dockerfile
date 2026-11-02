FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY src/SeniorDevAssignments.Core/SeniorDevAssignments.Core.csproj SeniorDevAssignments.Core/
COPY src/SeniorDevAssignments.Web/SeniorDevAssignments.Web.csproj SeniorDevAssignments.Web/
RUN dotnet restore SeniorDevAssignments.Web/SeniorDevAssignments.Web.csproj
COPY src/SeniorDevAssignments.Core/ SeniorDevAssignments.Core/
COPY src/SeniorDevAssignments.Web/ SeniorDevAssignments.Web/
RUN dotnet publish SeniorDevAssignments.Web/SeniorDevAssignments.Web.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "SeniorDevAssignments.Web.dll"]
