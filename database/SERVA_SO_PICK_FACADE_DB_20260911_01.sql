IF OBJECT_ID(N'dbo.ServaLabelPrintRequest', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ServaLabelPrintRequest
    (
        CompanyID int NOT NULL,
        RequestID int IDENTITY(1,1) NOT NULL,
        Source varchar(12) NULL,
        OriginalRequestID int NULL,
        ShipmentNbr varchar(15) NULL,
        OrderType varchar(2) NULL,
        OrderNbr varchar(15) NULL,
        SODate date NULL,
        CompanyName nvarchar(60) NULL,
        CompanyCountry varchar(2) NULL,
        LineNbr int NULL,
        InventoryID int NULL,
        ItemNumber nvarchar(30) NULL,
        ItemDescription nvarchar(255) NULL,
        SiteID int NULL,
        SiteName nvarchar(60) NULL,
        LocationID int NULL,
        LocationCD nvarchar(100) NULL,
        ContainerID nvarchar(30) NULL,
        ShipLocationCD nvarchar(30) NULL,
        UOM varchar(6) NULL,
        PickedQty decimal(25,6) NULL,
        QuantityPerLabel decimal(25,6) NULL,
        LabelCount int NULL,
        CountryOfOrigin varchar(2) NULL,
        PrinterName nvarchar(255) NULL,
        PrinterID uniqueidentifier NULL,
        ZplFileID uniqueidentifier NULL,
        ZplFileName nvarchar(255) NULL,
        Status varchar(12) NULL,
        ReprintReason nvarchar(255) NULL,
        ErrorMessage nvarchar(2048) NULL,
        CreatedByID uniqueidentifier NULL,
        CreatedDateTime datetime NULL,
        LastModifiedByID uniqueidentifier NULL,
        LastModifiedDateTime datetime NULL,
        tstamp timestamp NOT NULL,
        CONSTRAINT PK_ServaLabelPrintRequest PRIMARY KEY CLUSTERED (CompanyID, RequestID)
    );
END
GO

IF COL_LENGTH(N'dbo.ServaLabelPrintRequest', N'OriginalRequestID') IS NULL
    ALTER TABLE dbo.ServaLabelPrintRequest ADD OriginalRequestID int NULL;
GO
IF COL_LENGTH(N'dbo.ServaLabelPrintRequest', N'ReprintReason') IS NULL
    ALTER TABLE dbo.ServaLabelPrintRequest ADD ReprintReason nvarchar(255) NULL;
GO
IF COL_LENGTH(N'dbo.ServaLabelPrintRequest', N'QuantityPerLabel') IS NULL
    ALTER TABLE dbo.ServaLabelPrintRequest ADD QuantityPerLabel decimal(25,6) NULL;
GO
IF COL_LENGTH(N'dbo.ServaLabelPrintRequest', N'LastModifiedByID') IS NULL
    ALTER TABLE dbo.ServaLabelPrintRequest ADD LastModifiedByID uniqueidentifier NULL;
GO
IF COL_LENGTH(N'dbo.ServaLabelPrintRequest', N'LastModifiedDateTime') IS NULL
    ALTER TABLE dbo.ServaLabelPrintRequest ADD LastModifiedDateTime datetime NULL;
GO
IF COL_LENGTH(N'dbo.ServaLabelPrintRequest', N'SODate') IS NULL
    ALTER TABLE dbo.ServaLabelPrintRequest ADD SODate date NULL;
GO
IF COL_LENGTH(N'dbo.ServaLabelPrintRequest', N'CompanyName') IS NULL
    ALTER TABLE dbo.ServaLabelPrintRequest ADD CompanyName nvarchar(60) NULL;
GO
IF COL_LENGTH(N'dbo.ServaLabelPrintRequest', N'CompanyCountry') IS NULL
    ALTER TABLE dbo.ServaLabelPrintRequest ADD CompanyCountry varchar(2) NULL;
GO
IF COL_LENGTH(N'dbo.ServaLabelPrintRequest', N'ZplFileID') IS NULL
    ALTER TABLE dbo.ServaLabelPrintRequest ADD ZplFileID uniqueidentifier NULL;
GO
IF COL_LENGTH(N'dbo.ServaLabelPrintRequest', N'ZplFileName') IS NULL
    ALTER TABLE dbo.ServaLabelPrintRequest ADD ZplFileName nvarchar(255) NULL;
GO
