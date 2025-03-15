# Adjust DOTNET_OS_VERSION as desired
ARG DOTNET_OS_VERSION="-alpine"
ARG DOTNET_SDK_VERSION=9.0

FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_SDK_VERSION}${DOTNET_OS_VERSION} AS build
WORKDIR /src

# copy everything
COPY . ./
# restore as distinct layers
RUN dotnet restore
# build and publish a release
RUN dotnet publish -c Release -r linux-x64 --property:PublishDir="/app"

# final stage/image
FROM mcr.microsoft.com/dotnet/aspnet:${DOTNET_SDK_VERSION}
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080
COPY --from=build /app /app
ENTRYPOINT [ "/app/pbin" ]
