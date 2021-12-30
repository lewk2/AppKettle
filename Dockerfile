#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS base
WORKDIR /app
EXPOSE 80

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src
COPY ["AppKettle/AppKettle.csproj", "AppKettle/"]
RUN dotnet restore "AppKettle/AppKettle.csproj"
COPY . .
WORKDIR "/src/AppKettle"
RUN dotnet build "AppKettle.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "AppKettle.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "AppKettle.dll"]