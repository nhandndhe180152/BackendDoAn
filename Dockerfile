# Giai đoạn 1: Chạy môi trường Runtime siêu nhẹ
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_HTTP_PORTS=8080

# Cài đặt font DejaVu để hỗ trợ hiển thị tiếng Việt khi xuất PDF
RUN apt-get update && apt-get install -y --no-install-recommends \
    fonts-dejavu-core \
    && rm -rf /var/lib/apt/lists/*


# Giai đoạn 2: Dùng SDK để build code
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy file .sln và tất cả các file .csproj của từng layer
# (Lưu ý: Thường không cần copy project UnitTest khi build đem đi deploy)
COPY ["Backend.sln", "./"]
COPY ["Backend.API/Backend.API.csproj", "Backend.API/"]
COPY ["Backend.Application/Backend.Application.csproj", "Backend.Application/"]
COPY ["Backend.Domain/Backend.Domain.csproj", "Backend.Domain/"]
COPY ["Backend.Infrastructure/Backend.Infrastructure.csproj", "Backend.Infrastructure/"]
COPY ["Backend.Share/Backend.Share.csproj", "Backend.Share/"]
COPY ["Backend.UnitTest/Backend.UnitTest.csproj", "Backend.UnitTest/"]

# Restore toàn bộ thư viện thông qua file .sln
RUN dotnet restore "Backend.sln"

# Copy toàn bộ phần code còn lại của tất cả các thư mục vào
COPY . .

# Chuyển thư mục làm việc vào project chính (API) để tiến hành build
WORKDIR "/src/Backend.API"
RUN dotnet build "Backend.API.csproj" -c Release -o /app/build

# Giai đoạn 3: Publish project API
FROM build AS publish
RUN dotnet publish "Backend.API.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Giai đoạn 4: Chạy ứng dụng từ thư mục publish
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Lệnh khởi chạy file .dll của project chính
ENTRYPOINT ["dotnet", "Backend.API.dll"]