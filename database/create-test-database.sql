-- Chỉ chạy bằng sqlcmd; tên database thử nghiệm cố định, dừng nếu đã tồn tại.
:On Error exit
USE master;
GO
IF DB_ID(N'QuanLyTaiSanHexa_DotNet_Test') IS NOT NULL
    THROW 51000, 'Test database already exists. Restore a copy or choose another empty database.', 1;
CREATE DATABASE QuanLyTaiSanHexa_DotNet_Test;
GO
USE QuanLyTaiSanHexa_DotNet_Test;
GO
CREATE TABLE dbo.Departments
(
    Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Departments PRIMARY KEY,
    DepartmentCode NVARCHAR(50) NOT NULL CONSTRAINT UQ_Departments_Code UNIQUE,
    DepartmentName NVARCHAR(255) NOT NULL,
    Description NVARCHAR(500) NULL,
    CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_Departments_CreatedAt DEFAULT SYSDATETIME()
);
GO

CREATE TABLE dbo.AssetCategories
(
    Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_AssetCategories PRIMARY KEY,
    CategoryCode NVARCHAR(50) NOT NULL CONSTRAINT UQ_AssetCategories_Code UNIQUE,
    CategoryName NVARCHAR(255) NOT NULL,
    Description NVARCHAR(500) NULL,
    CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_Categories_CreatedAt DEFAULT SYSDATETIME()
);
GO

CREATE TABLE dbo.Assets
(
    Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Assets PRIMARY KEY,
    AssetCode NVARCHAR(100) NOT NULL CONSTRAINT UQ_Assets_AssetCode UNIQUE,
    AssetName NVARCHAR(255) NOT NULL,
    CategoryId INT NULL,
    DepartmentId INT NULL,
    SerialNumber NVARCHAR(255) NULL,
    PurchaseDate DATE NULL,
    PurchasePrice DECIMAL(18,2) NULL,
    Status NVARCHAR(30) NOT NULL CONSTRAINT DF_Assets_Status DEFAULT N'available',
    Description NVARCHAR(1000) NULL,
    ImageUrl NVARCHAR(1000) NULL,
    CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_Assets_CreatedAt DEFAULT SYSDATETIME(),
    UpdatedAt DATETIME2 NULL,
    CONSTRAINT FK_Assets_Category FOREIGN KEY (CategoryId) REFERENCES dbo.AssetCategories(Id),
    CONSTRAINT FK_Assets_Department FOREIGN KEY (DepartmentId) REFERENCES dbo.Departments(Id),
    CONSTRAINT CK_Assets_Status CHECK (Status IN (N'available', N'using', N'maintenance', N'broken', N'liquidated')),
    CONSTRAINT CK_Assets_PurchasePrice CHECK (PurchasePrice IS NULL OR PurchasePrice >= 0)
);
GO

CREATE INDEX IX_Assets_CategoryId ON dbo.Assets(CategoryId);
CREATE INDEX IX_Assets_DepartmentId ON dbo.Assets(DepartmentId);
GO


/*
  Nâng cấp V2 - KHÔNG xóa dữ liệu hiện có.
  Chạy file này một lần trong SSMS trước khi chạy phiên bản mới.
*/
USE QuanLyTaiSanHexa_DotNet_Test;
GO

IF COL_LENGTH(N'dbo.Assets', N'AssignedTo') IS NULL
BEGIN
    ALTER TABLE dbo.Assets ADD AssignedTo NVARCHAR(255) NULL;
END;
GO

IF COL_LENGTH(N'dbo.Assets', N'IpAddress') IS NULL
BEGIN
    ALTER TABLE dbo.Assets ADD IpAddress NVARCHAR(50) NULL;
END;
GO

IF NOT EXISTS
(
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_Assets_Status' AND object_id = OBJECT_ID(N'dbo.Assets')
)
BEGIN
    CREATE INDEX IX_Assets_Status ON dbo.Assets(Status);
END;
GO

IF NOT EXISTS
(
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_Assets_AssignedTo' AND object_id = OBJECT_ID(N'dbo.Assets')
)
BEGIN
    CREATE INDEX IX_Assets_AssignedTo ON dbo.Assets(AssignedTo);
END;
GO

SELECT TOP (20) Id, AssetCode, AssetName, AssignedTo, IpAddress, Status
FROM dbo.Assets ORDER BY Id DESC;
GO

/*
  Nâng cấp V3 - quản lý tài khoản người dùng.
  Script an toàn, không xóa dữ liệu hiện có.
*/
USE QuanLyTaiSanHexa_DotNet_Test;
GO

IF OBJECT_ID(N'dbo.Users', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Users
    (
        Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Users PRIMARY KEY,
        EmployeeCode NVARCHAR(50) NULL,
        FullName NVARCHAR(255) NOT NULL,
        Email NVARCHAR(255) NOT NULL,
        PasswordHash NVARCHAR(255) NOT NULL,
        Role NVARCHAR(30) NOT NULL CONSTRAINT DF_Users_Role DEFAULT N'employee',
        DepartmentId INT NULL,
        IsActive BIT NOT NULL CONSTRAINT DF_Users_IsActive DEFAULT 1,
        CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_Users_CreatedAt DEFAULT SYSDATETIME(),
        UpdatedAt DATETIME2 NULL,
        CONSTRAINT UQ_Users_Email UNIQUE (Email),
        CONSTRAINT CK_Users_Role CHECK (Role IN (N'admin', N'employee')),
        CONSTRAINT FK_Users_Department FOREIGN KEY (DepartmentId) REFERENCES dbo.Departments(Id)
    );
END;
GO

IF COL_LENGTH(N'dbo.Users', N'EmployeeCode') IS NULL
BEGIN
    ALTER TABLE dbo.Users ADD EmployeeCode NVARCHAR(50) NULL;
END;
GO

IF NOT EXISTS
(
    SELECT 1 FROM sys.indexes
    WHERE name = N'UX_Users_EmployeeCode'
      AND object_id = OBJECT_ID(N'dbo.Users')
)
BEGIN
    CREATE UNIQUE INDEX UX_Users_EmployeeCode
    ON dbo.Users(EmployeeCode)
    WHERE EmployeeCode IS NOT NULL;
END;
GO

IF NOT EXISTS
(
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_Users_DepartmentId'
      AND object_id = OBJECT_ID(N'dbo.Users')
)
BEGIN
    CREATE INDEX IX_Users_DepartmentId ON dbo.Users(DepartmentId);
END;
GO

SELECT Id, EmployeeCode, FullName, Email, Role, DepartmentId, IsActive, CreatedAt
FROM dbo.Users ORDER BY Id;
GO

/*
  Nâng cấp V4 - bàn giao, thu hồi và lịch sử tài sản.
  Script không xóa dữ liệu hiện có.
*/
USE QuanLyTaiSanHexa_DotNet_Test;
GO

IF COL_LENGTH(N'dbo.Assets', N'AssignedUserId') IS NULL
BEGIN
    ALTER TABLE dbo.Assets ADD AssignedUserId INT NULL;
END;
GO

IF NOT EXISTS
(
    SELECT 1 FROM sys.foreign_keys
    WHERE name = N'FK_Assets_AssignedUser'
)
BEGIN
    ALTER TABLE dbo.Assets
    ADD CONSTRAINT FK_Assets_AssignedUser
        FOREIGN KEY (AssignedUserId) REFERENCES dbo.Users(Id);
END;
GO

IF OBJECT_ID(N'dbo.AssetAssignments', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.AssetAssignments
    (
        Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_AssetAssignments PRIMARY KEY,
        AssetId INT NOT NULL,
        UserId INT NOT NULL,
        AssignedToSnapshot NVARCHAR(305) NOT NULL,
        AssignedDate DATE NOT NULL,
        ReturnedDate DATE NULL,
        AssignNote NVARCHAR(1000) NULL,
        ReturnNote NVARCHAR(1000) NULL,
        AssignedBy INT NULL,
        ReturnedBy INT NULL,
        IsActive BIT NOT NULL CONSTRAINT DF_AssetAssignments_IsActive DEFAULT 1,
        CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_AssetAssignments_CreatedAt DEFAULT SYSDATETIME(),
        UpdatedAt DATETIME2 NULL,
        CONSTRAINT FK_Assignments_Asset FOREIGN KEY (AssetId) REFERENCES dbo.Assets(Id),
        CONSTRAINT FK_Assignments_User FOREIGN KEY (UserId) REFERENCES dbo.Users(Id),
        CONSTRAINT FK_Assignments_AssignedBy FOREIGN KEY (AssignedBy) REFERENCES dbo.Users(Id),
        CONSTRAINT FK_Assignments_ReturnedBy FOREIGN KEY (ReturnedBy) REFERENCES dbo.Users(Id)
    );
END;
GO

IF NOT EXISTS
(
    SELECT 1 FROM sys.indexes
    WHERE name = N'UX_AssetAssignments_Active'
      AND object_id = OBJECT_ID(N'dbo.AssetAssignments')
)
BEGIN
    CREATE UNIQUE INDEX UX_AssetAssignments_Active
    ON dbo.AssetAssignments(AssetId)
    WHERE IsActive = 1;
END;
GO

IF OBJECT_ID(N'dbo.AssetHistory', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.AssetHistory
    (
        Id BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_AssetHistory PRIMARY KEY,
        AssetId INT NULL,
        AssetCode NVARCHAR(100) NOT NULL,
        ActionType NVARCHAR(30) NOT NULL,
        Description NVARCHAR(1000) NOT NULL,
        OldData NVARCHAR(MAX) NULL,
        NewData NVARCHAR(MAX) NULL,
        PerformedBy INT NULL,
        CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_AssetHistory_CreatedAt DEFAULT SYSDATETIME(),
        CONSTRAINT FK_AssetHistory_Asset FOREIGN KEY (AssetId)
            REFERENCES dbo.Assets(Id) ON DELETE SET NULL,
        CONSTRAINT FK_AssetHistory_User FOREIGN KEY (PerformedBy)
            REFERENCES dbo.Users(Id)
    );
END;
GO

IF NOT EXISTS
(
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_AssetHistory_AssetId_CreatedAt'
      AND object_id = OBJECT_ID(N'dbo.AssetHistory')
)
BEGIN
    CREATE INDEX IX_AssetHistory_AssetId_CreatedAt
    ON dbo.AssetHistory(AssetId, CreatedAt DESC);
END;
GO

SELECT N'Nâng cấp V4 thành công.' AS Message;
GO

/*
  V4.1 - cho phép bàn giao bằng tên tự nhập, không bắt buộc tài khoản.
  Không xóa hoặc thay đổi các lượt bàn giao hiện có.
*/
USE QuanLyTaiSanHexa_DotNet_Test;
GO

IF OBJECT_ID(N'dbo.AssetAssignments', N'U') IS NOT NULL
BEGIN
    ALTER TABLE dbo.AssetAssignments ALTER COLUMN UserId INT NULL;
END;
GO

SELECT N'Nâng cấp V4.1 thành công.' AS Message;
GO

USE QuanLyTaiSanHexa_DotNet_Test;
GO

IF COL_LENGTH(N'dbo.Assets', N'PublicId') IS NULL
BEGIN
    ALTER TABLE dbo.Assets ADD PublicId UNIQUEIDENTIFIER NULL;
END;
GO

-- Mỗi bước dùng một batch riêng để SQL Server nhận diện cột vừa tạo.
UPDATE dbo.Assets SET PublicId = NEWID() WHERE PublicId IS NULL;
GO

IF EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID(N'dbo.Assets') AND name = N'PublicId' AND is_nullable = 1
)
    ALTER TABLE dbo.Assets ALTER COLUMN PublicId UNIQUEIDENTIFIER NOT NULL;
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.default_constraints dc
    JOIN sys.columns c ON c.object_id = dc.parent_object_id AND c.column_id = dc.parent_column_id
    WHERE dc.parent_object_id = OBJECT_ID(N'dbo.Assets') AND c.name = N'PublicId'
)
    ALTER TABLE dbo.Assets ADD CONSTRAINT DF_Assets_PublicId DEFAULT NEWID() FOR PublicId;
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.Assets') AND name = N'UX_Assets_PublicId'
)
    CREATE UNIQUE INDEX UX_Assets_PublicId ON dbo.Assets(PublicId);
GO

IF OBJECT_ID(N'dbo.InstalledSoftware', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.InstalledSoftware
    (
        Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_InstalledSoftware PRIMARY KEY,
        AssetId INT NOT NULL,
        SoftwareName NVARCHAR(255) NOT NULL,
        Version NVARCHAR(100) NULL,
        Publisher NVARCHAR(255) NULL,
        InstalledDate DATE NULL,
        Notes NVARCHAR(500) NULL,
        CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_InstalledSoftware_CreatedAt DEFAULT SYSDATETIME(),
        UpdatedAt DATETIME2 NULL,
        CONSTRAINT FK_InstalledSoftware_Asset FOREIGN KEY (AssetId) REFERENCES dbo.Assets(Id) ON DELETE CASCADE
    );
    CREATE INDEX IX_InstalledSoftware_AssetId ON dbo.InstalledSoftware(AssetId);
END;
GO

UPDATE dbo.Assets
SET Status = CASE
    WHEN AssignedTo IS NOT NULL AND LTRIM(RTRIM(AssignedTo))<>N'' THEN N'using'
    ELSE N'available'
END
WHERE Status IN (N'available', N'using');
GO

SELECT N'Nâng cấp V5 thành công.' AS Message;
GO

USE QuanLyTaiSanHexa_DotNet_Test;
GO

IF OBJECT_ID(N'dbo.AssetImages', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.AssetImages
    (
        Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_AssetImages PRIMARY KEY,
        AssetId INT NOT NULL,
        FileName NVARCHAR(255) NOT NULL,
        MimeType VARCHAR(50) NOT NULL,
        FileSize INT NOT NULL,
        ImageData VARBINARY(MAX) NOT NULL,
        IsPrimary BIT NOT NULL CONSTRAINT DF_AssetImages_IsPrimary DEFAULT 0,
        UploadedBy INT NULL,
        CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_AssetImages_CreatedAt DEFAULT SYSDATETIME(),
        CONSTRAINT FK_AssetImages_Asset FOREIGN KEY (AssetId) REFERENCES dbo.Assets(Id) ON DELETE CASCADE,
        CONSTRAINT FK_AssetImages_UploadedBy FOREIGN KEY (UploadedBy) REFERENCES dbo.Users(Id),
        CONSTRAINT CK_AssetImages_FileSize CHECK (FileSize > 0 AND FileSize <= 5242880),
        CONSTRAINT CK_AssetImages_MimeType CHECK (MimeType IN ('image/jpeg','image/png','image/webp'))
    );
    CREATE INDEX IX_AssetImages_AssetId ON dbo.AssetImages(AssetId, IsPrimary DESC, Id);
END;
GO

SELECT N'Nâng cấp V6 - hình ảnh tài sản thành công.' AS Message;
GO

ALTER TABLE dbo.Users ADD TokenVersion INT NOT NULL CONSTRAINT DF_Users_TokenVersion DEFAULT 0;
GO
