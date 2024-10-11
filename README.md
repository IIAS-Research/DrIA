dotnet user-secrets init

dotnet user-secrets set "Dr_IA:PathMainStorage" "/home/jules/Dev/dr_ia/dr-ia/Server/Storage"
dotnet user-secrets set "Dr_IA:JwtPrivateKey" "069450480169876980d86e40684054de40c5a454d5e4"
dotnet user-secrets set "Dr_IA:ConnectionString" "Host=localhost;Database=dr_ia_dev;Username=jules;Password=1234"

dotnet watch

# https://learn.microsoft.com/en-us/aspnet/core/security/app-secrets?view=aspnetcore-8.0&tabs=linux
# https://learn.microsoft.com/en-us/aspnet/core/?view=aspnetcore-8.0
F5 au pire

npm i dans le client
quasar d