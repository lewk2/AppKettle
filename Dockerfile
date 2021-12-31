#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS base
WORKDIR /app
EXPOSE 5000

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src
COPY ["AppKettle.Service/AppKettle.Service.csproj", "AppKettle.Service/"]
RUN dotnet restore "AppKettle.Service/AppKettle.Service.csproj"
COPY . .
WORKDIR "/src/AppKettle.Service"
RUN dotnet build "AppKettle.Service.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "AppKettle.Service.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "AppKettle.Service.dll"]