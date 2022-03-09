// Copyright (c) Brock Allen & Dominick Baier. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.


using System;
using IdentityServer4.Models;
using System.Collections.Generic;
using IdentityServer4;

namespace Dwh.IS4Host
{
    public static class Config
    {
        public static IEnumerable<IdentityResource> IdentityResources =>
            new IdentityResource[]
            {
                new IdentityResources.OpenId(),
                new IdentityResources.Profile(),
            };

        public static IEnumerable<ApiScope> ApiScopes =>
            new ApiScope[]
            {
                new ApiScope("apiApp", "DWH Portal"),
            };

        public static IEnumerable<Client> Clients =>
            new Client[]
            {
                // m2m client credentials flow client
                new Client
                {
                    ClientId = "dwh.spa",
                    ClientName = "DWH Portal Frontend",
                    ClientUri = String.Empty,
                    RequireClientSecret = false,
                    RequireConsent = false,
                    AllowedGrantTypes = GrantTypes.Implicit,
                    RedirectUris = new List<string>(),
                    PostLogoutRedirectUris = new List<string>(),
                    AllowedCorsOrigins = new List<string>(),
                    AllowAccessTokensViaBrowser = true,
                    AlwaysIncludeUserClaimsInIdToken = true,
                    AlwaysSendClientClaims = true,
                    AccessTokenLifetime = 3600,
                    AllowedScopes =
                    {
                        IdentityServerConstants.StandardScopes.OpenId,
                        IdentityServerConstants.StandardScopes.Profile,
                        IdentityServerConstants.StandardScopes.Email,
                        "apiApp"
                    },
                },
                new Client()
                {
                    ClientName = "Adhoc MCV Client",
                    ClientId = "adhoc-client",
                    AllowedGrantTypes = GrantTypes.Hybrid,
                    RedirectUris = new List<string>(),
                    RequirePkce = false,
                    AllowedScopes =
                    {
                        IdentityServerConstants.StandardScopes.OpenId,
                        IdentityServerConstants.StandardScopes.Profile,
                        IdentityServerConstants.StandardScopes.Email,
                    },
                    ClientSecrets = {new Secret("a79fd782-bd4b-45ac-b13d-03e99d89b186".Sha512())}
                },
                new Client
                {
                    ClientId = "nascop.spa",
                    ClientName = "NASCOP DWH Portal Frontend",
                    ClientUri = String.Empty,
                    RequireClientSecret = false,
                    RequireConsent = false,
                    AllowedGrantTypes = GrantTypes.Implicit,
                    RedirectUris = new List<string>(),
                    PostLogoutRedirectUris = new List<string>(),
                    AllowedCorsOrigins = new List<string>(),
                    AllowAccessTokensViaBrowser = true,
                    AlwaysIncludeUserClaimsInIdToken = true,
                    AlwaysSendClientClaims = true,
                    AccessTokenLifetime = 3600,
                    AllowedScopes =
                    {
                        IdentityServerConstants.StandardScopes.OpenId,
                        IdentityServerConstants.StandardScopes.Profile,
                        IdentityServerConstants.StandardScopes.Email,
                        "apiApp"
                    },
                },
                new Client()
                {
                    ClientId = "nascop.adhoc-client",
                    ClientName = "NASCOP Adhoc MCV Client",
                    AllowedGrantTypes = GrantTypes.Hybrid,
                    RedirectUris = new List<string>(),
                    RequirePkce = false,
                    AllowedScopes =
                    {
                        IdentityServerConstants.StandardScopes.OpenId,
                        IdentityServerConstants.StandardScopes.Profile,
                        IdentityServerConstants.StandardScopes.Email,
                    },
                    ClientSecrets = { new Secret("a78fd782-bd4b-45ac-b13d-03e99d89b186".Sha512()) }
                },
                new Client
                {
                    ClientId = "dwh.his",
                    ClientName = "DWH HIS",
                    ClientUri = String.Empty,
                    RequireClientSecret = false,
                    RequireConsent = false,
                    AllowedGrantTypes = GrantTypes.Implicit,
                    RedirectUris = new List<string>(),
                    PostLogoutRedirectUris = new List<string>(),
                    AllowedCorsOrigins = new List<string>(),
                    AllowAccessTokensViaBrowser = true,
                    AlwaysIncludeUserClaimsInIdToken = true,
                    AlwaysSendClientClaims = true,
                    AccessTokenLifetime = 3600,
                    AllowedScopes =
                    {
                        IdentityServerConstants.StandardScopes.OpenId,
                        IdentityServerConstants.StandardScopes.Profile,
                        IdentityServerConstants.StandardScopes.Email,
                        "apiApp"
                    },
                },
            };
    }
}
