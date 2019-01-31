FROM microsoft/dotnet:2.1-aspnetcore-runtime AS base
WORKDIR /app
EXPOSE 61007
EXPOSE 44312

FROM microsoft/dotnet:2.1-sdk AS build
WORKDIR /src
COPY OneRosterSync.Net/OneRosterSync.Net.csproj OneRosterSync.Net/
RUN dotnet restore OneRosterSync.Net/OneRosterSync.Net.csproj
COPY . .
WORKDIR /src/OneRosterSync.Net
RUN dotnet build OneRosterSync.Net.csproj -c Release -o /app

FROM build AS publish
RUN dotnet publish OneRosterSync.Net.csproj -c Release -o /app

FROM base AS final
WORKDIR /app
COPY --from=publish /app .
ENTRYPOINT ["dotnet", "OneRosterSync.Net.dll"]
