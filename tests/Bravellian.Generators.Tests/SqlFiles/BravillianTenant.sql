CREATE TABLE [pw].[BravellianTenant] (
    [BravellianTenantId] BIGINT NOT NULL,
    [Name]              NVARCHAR (200)   NOT NULL,
    [OrganizationId]    BIGINT NOT NULL,
    CONSTRAINT [PK_BravellianTenant] PRIMARY KEY ([BravellianTenantId] ASC),
    CONSTRAINT [FK_pw_BravellianTenant_Organization] FOREIGN KEY ([OrganizationId]) REFERENCES [pw].[Organization] ([OrganizationId])
);

