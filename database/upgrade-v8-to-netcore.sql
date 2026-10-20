-- Chọn bản SAO database V8 bằng -d khi chạy sqlcmd. Không đổi tên bảng/cột, không xóa dữ liệu.
:On Error exit
SET XACT_ABORT ON;
IF OBJECT_ID(N'dbo.AssetImages',N'U') IS NULL
 OR OBJECT_ID(N'dbo.InstalledSoftware',N'U') IS NULL
 OR OBJECT_ID(N'dbo.AssetAssignments',N'U') IS NULL
 OR OBJECT_ID(N'dbo.AssetHistory',N'U') IS NULL
 OR COL_LENGTH(N'dbo.Assets',N'PublicId') IS NULL
 OR COL_LENGTH(N'dbo.Users',N'PasswordHash') IS NULL
    THROW 51000, 'Expected V8 schema. Stop and compare the source database schema.',1;
BEGIN TRANSACTION;
IF COL_LENGTH(N'dbo.Users',N'TokenVersion') IS NULL
    ALTER TABLE dbo.Users ADD TokenVersion INT NOT NULL CONSTRAINT DF_Users_TokenVersion DEFAULT 0;
COMMIT;
GO
SELECT DB_NAME() DatabaseName, COUNT(*) UserCount FROM dbo.Users;
SELECT COUNT(*) AssetCount FROM dbo.Assets;
GO
