FROM mcr.microsoft.com/dotnet/core/aspnet:3.1.2 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/core/sdk:3.1.200 AS publish
WORKDIR /sln
COPY NuGet.Config .
COPY packages/ ./packages
COPY ["src/DemoFluffySpoon.UserVerification/DemoFluffySpoon.UserVerification.csproj", "src/DemoFluffySpoon.UserVerification/"]
RUN dotnet restore "src/DemoFluffySpoon.UserVerification/DemoFluffySpoon.UserVerification.csproj"
COPY . .
WORKDIR "src/DemoFluffySpoon.UserVerification"
ARG VERSION=1
RUN dotnet publish "DemoFluffySpoon.UserVerification.csproj" /p:Version=$VERSION -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "DemoFluffySpoon.UserVerification.dll"]