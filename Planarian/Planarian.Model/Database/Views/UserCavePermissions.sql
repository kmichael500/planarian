CREATE
OR REPLACE VIEW "UserCavePermissions" AS
SELECT cp."AccountId",
       cp."UserId",
       c."Id" AS "CaveId",
       NULL::character varying AS "CountyId",
       p."Key"                 AS "PermissionKey",
       p."Id"                  AS "PermissionId",
       c."StateId"             AS "StateId"
FROM "CavePermission" cp
    JOIN "Caves" c
ON c."AccountId"::text = cp."AccountId"::text AND
    (cp."CaveId" IS NOT NULL AND cp."CaveId"::text = c."Id"::text OR
    cp."CountyId" IS NOT NULL AND cp."CountyId"::text = c."CountyId"::text OR
    cp."StateId" IS NOT NULL AND cp."StateId"::text = c."StateId"::text OR
    cp."CaveId" IS NULL AND cp."CountyId" IS NULL AND cp."StateId" IS NULL)
    JOIN "Permissions" p ON cp."PermissionId"::text = p."Id"::text
WHERE p."PermissionType"::text = 'Cave'::text
UNION
SELECT cp."AccountId",
       cp."UserId",
       NULL::character varying AS "CaveId",
       ct."Id"                 AS "CountyId",
       p."Key"                 AS "PermissionKey",
       p."Id"                  AS "PermissionId",
       ct."StateId"            AS "StateId"
FROM "CavePermission" cp
    JOIN "Counties" ct
ON ct."AccountId"::text = cp."AccountId"::text AND
    (cp."CountyId" IS NOT NULL AND cp."CountyId"::text = ct."Id"::text OR
    cp."StateId" IS NOT NULL AND cp."StateId"::text = ct."StateId"::text OR
    cp."CaveId" IS NULL AND cp."CountyId" IS NULL AND cp."StateId" IS NULL)
    JOIN "Permissions" p ON cp."PermissionId"::text = p."Id"::text
WHERE p."PermissionType"::text = 'Cave'::text
UNION
SELECT up."AccountId",
       up."UserId",
       c."Id" AS "CaveId",
       NULL::character varying AS "CountyId",
       p."Key"                 AS "PermissionKey",
       p."Id"                  AS "PermissionId",
       c."StateId"             AS "StateId"
FROM "UserPermissions" up
    JOIN "Caves" c
ON c."AccountId"::text = up."AccountId"::text
    CROSS JOIN "Permissions" p
WHERE (up."PermissionId"::text IN (SELECT "Permissions"."Id"
    FROM "Permissions"
    WHERE "Permissions"."Key"::text = 'Admin'::text))
  AND p."PermissionType"::text = 'Cave'::text
UNION
SELECT up."AccountId",
       up."UserId",
       NULL::character varying AS "CaveId",
       ct."Id"                 AS "CountyId",
       p."Key"                 AS "PermissionKey",
       p."Id"                  AS "PermissionId",
       ct."StateId"            AS "StateId"
FROM "UserPermissions" up
    JOIN "Counties" ct
ON ct."AccountId"::text = up."AccountId"::text
    CROSS JOIN "Permissions" p
WHERE (up."PermissionId"::text IN (SELECT "Permissions"."Id"
    FROM "Permissions"
    WHERE "Permissions"."Key"::text = 'Admin'::text))
  AND p."PermissionType"::text = 'Cave'::text;
