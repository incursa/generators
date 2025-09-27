CREATE TABLE [pw].[Organization] (
    -- Core Organization Fields
    [OrganizationId]         BIGINT NOT NULL,
    [Name]                   NVARCHAR (200)   NOT NULL,
    [DbConnectionString]     NVARCHAR (400)   NOT NULL,
    [GlobalAdminUserId]      VARCHAR(100)     NOT NULL,
    [OnboardedOn]            DATETIMEOFFSET(7)     NULL,

    -- Subscription Fields
    [SubscriptionStatus]     VARCHAR(20)      NOT NULL CONSTRAINT [DF_pw_Organization_SubscriptionStatus] DEFAULT ('pending'),
    [ExternalSubscriptionId] VARCHAR(100)     NULL,
    [SubscriptionProvider]   VARCHAR(50)      NOT NULL CONSTRAINT [DF_pw_Organization_SubscriptionProvider] DEFAULT ('internal'),
    [SubscriptionStartOn]    DATETIMEOFFSET(7)     NULL,
    [SubscriptionEndOn]      DATETIMEOFFSET(7)     NULL,

    CONSTRAINT [PK_pw_Organization] PRIMARY KEY ([OrganizationId] ASC),
    CONSTRAINT [CK_Organization_SubscriptionStatus]
        CHECK ([SubscriptionStatus] IN ('active', 'suspended', 'cancelled', 'pending')),
    CONSTRAINT [CK_Organization_SubscriptionProvider]
        CHECK ([SubscriptionProvider] IN ('msft_marketplace', 'stripe', 'internal'))
);

