CREATE TABLE [pw].[OrganizationUser] (
    [OrganizationUserId]  BIGINT NOT NULL,
    [OrganizationId]      BIGINT NOT NULL,
    [UserId]              VARCHAR(100)     NOT NULL,
    [IsActive]            BIT              NOT NULL CONSTRAINT [DF_pw_OrganizationUser_IsActive] DEFAULT (1),
    [RecordSource]        VARCHAR(20)      NOT NULL CONSTRAINT [DF_pw_OrganizationUser_RecordSource] DEFAULT ('system'),

    CONSTRAINT [PK_pw_OrganizationUser] PRIMARY KEY ([OrganizationUserId] ASC),
    CONSTRAINT [FK_pw_OrganizationUser_Organization] FOREIGN KEY ([OrganizationId]) REFERENCES [pw].[Organization] ([OrganizationId]),
    CONSTRAINT [FK_pw_OrganizationUser_User] FOREIGN KEY ([UserId]) REFERENCES [pw].[User] ([UserId]),
    CONSTRAINT [UQ_pw_OrganizationUser_OrganizationId_UserId] UNIQUE ([OrganizationId], [UserId]),
    CONSTRAINT [CK_pw_OrganizationUser_RecordSource] CHECK ([RecordSource] IN ('manual', 'erp', 'system'))
);
