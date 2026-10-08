IF COL_LENGTH(N'dbo.ServaLabelPrintRequest', N'QuantityPerLabel') IS NULL
    ALTER TABLE dbo.ServaLabelPrintRequest ADD QuantityPerLabel decimal(25,6) NULL;
GO
