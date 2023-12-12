FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443
EXPOSE 5048

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["MessageSample.csproj", "./"]
RUN dotnet restore "MessageSample.csproj"
COPY . .
WORKDIR "/src/"
RUN dotnet build "MessageSample.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "MessageSample.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "MessageSample.dll"]
