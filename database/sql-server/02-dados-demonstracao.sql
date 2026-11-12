/*
    Galeria Aurora - dados fictícios mínimos para demonstração.

    Este script é idempotente: só inclui o catálogo quando não encontra
    registros com os mesmos nomes. Não cria usuários e não contém dados pessoais.
*/

SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRANSACTION;

DECLARE @Today date = CONVERT(date, GETDATE());
DECLARE @StartDate date = DATEADD(day, 7, @Today);
DECLARE @EndDate date = DATEADD(day, 60, @Today);
DECLARE @ExposureId int;

IF NOT EXISTS (SELECT 1 FROM dbo.Exposicoes WHERE Titulo = N'Horizontes da Aurora')
BEGIN
    INSERT INTO dbo.Exposicoes
        (Titulo, Descricao, ImagemUrl, Data, StartDate, EndDate, IsActive)
    VALUES
        (N'Horizontes da Aurora',
         N'Exposição fictícia criada exclusivamente para a demonstração acadêmica do PIM IV.',
         N'/images/basquiat.jpeg',
         CONCAT(CONVERT(varchar(10), @StartDate, 103), N' a ', CONVERT(varchar(10), @EndDate, 103)),
         @StartDate, @EndDate, 1);
END;

SELECT @ExposureId = Id
FROM dbo.Exposicoes
WHERE Titulo = N'Horizontes da Aurora';

IF NOT EXISTS
(
    SELECT 1 FROM dbo.visit_sessions
    WHERE ExposureId = @ExposureId AND VisitDate = DATEADD(day, 7, @Today) AND StartTime = '10:00:00'
)
BEGIN
    INSERT INTO dbo.visit_sessions
        (ExposureId, VisitDate, StartTime, Capacity, ReservedQuantity, IsActive, Status, CancellationReason, CancelledAtUtc)
    VALUES
        (@ExposureId, DATEADD(day, 7, @Today), '10:00:00', 40, 0, 1, N'Open', NULL, NULL);
END;

IF NOT EXISTS
(
    SELECT 1 FROM dbo.visit_sessions
    WHERE ExposureId = @ExposureId AND VisitDate = DATEADD(day, 14, @Today) AND StartTime = '14:00:00'
)
BEGIN
    INSERT INTO dbo.visit_sessions
        (ExposureId, VisitDate, StartTime, Capacity, ReservedQuantity, IsActive, Status, CancellationReason, CancelledAtUtc)
    VALUES
        (@ExposureId, DATEADD(day, 14, @Today), '14:00:00', 40, 0, 1, N'Open', NULL, NULL);
END;

IF NOT EXISTS (SELECT 1 FROM dbo.Produtos WHERE Nome = N'Caneca Aurora')
BEGIN
    INSERT INTO dbo.Produtos (Nome, Preco, ImagemUrl, IsActive)
    VALUES (N'Caneca Aurora', 49.90, N'/images/caneca_aurora.jpeg', 1);
END;

IF NOT EXISTS (SELECT 1 FROM dbo.Produtos WHERE Nome = N'Tote Bag Aurora')
BEGIN
    INSERT INTO dbo.Produtos (Nome, Preco, ImagemUrl, IsActive)
    VALUES (N'Tote Bag Aurora', 69.90, N'/images/basquiat_tote_bag.webp', 1);
END;

IF NOT EXISTS (SELECT 1 FROM dbo.Produtos WHERE Nome = N'Caderno Artístico Aurora')
BEGIN
    INSERT INTO dbo.Produtos (Nome, Preco, ImagemUrl, IsActive)
    VALUES (N'Caderno Artístico Aurora', 39.90, N'/images/sn_x1000-pad_750x1000_f8f8f8.webp', 1);
END;

COMMIT;
