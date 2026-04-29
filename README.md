# Planarian

Planarian is meant to make collecting, managing, distributing, and using cave data as easy as possible for state cave surveys and other cave-data groups. It is built for groups that need to manage access-controlled cave data for more than one person, without requiring GIS experience.

Cave data is often in a spreadsheet, a poorly set up database like FileMaker, technical GIS software, or scattered around in different places. Then the files are stored somewhere else, usually something like Google Drive.

Part of the problem is that a lot of cave data also ends up in systems that were put together however someone happened to set them up at the time. **If that setup is messy, inconsistent, or hard to maintain, then working with the data gets harder for everyone after them.** Planarian helps avoid that by giving groups one place to store and manage the data in a structure that **stays usable and consistent over time**.

At the same time, the data should not be trapped in Planarian. If a group decides it no longer wants to use Planarian for whatever reason, admins can export an account archive with the cave data and related files in a consistent, easy-to-understand structure, making it much simpler to move the data somewhere else.

If someone wants to quickly look up information about a cave, they should not have to check multiple places. They should be able to search for the cave and be presented with the information in an easy-to-use format with the maps, files, and other relevant information **displayed in one place**.

If someone notices that a cave record is wrong or has a new cave to submit, the process should be simple. They should be able to focus on making the update or adding the cave instead of thinking about how to push the data through the process. People should not have to go back and forth in an email chain, manually pull the submission out of the email, find or create the record, make the change, and then re-distribute that updated data at a later date. Once the change is made, everyone with access should be able to see it right away. That makes the action feel worthwhile. When updates disappear into a slow process and nobody sees the change for a long time, contributing feels less useful and fewer people bother submitting updates.

## What Planarian is for

Planarian is not meant to be a public cave-finding website. Access is solely controlled by the group that manages the data.

It is also not meant to replace GIS software for people who need full GIS tools. The goal is to make the common cave-data tasks easier: finding caves, reviewing records, viewing maps and files, managing access, and submitting updates.

A typical workflow looks like this:

1. Import existing cave and entrance data.
2. Configure the fields, tags, counties, and permissions for the group.
3. Invite members, managers, or admins to manage the data.
4. Members can search, view records, use the map, and access files.
5. Members can suggest updates when they notice something wrong.
6. Managers/admins review changes so the data stays accurate.

## Data

Most of these fields can be enabled or disabled, so a group can decide what it wants to collect.

Many of the fields below use predefined lists of tags instead of free-form text. Things like geology, map status, field indication, and other common categories can be defined by admins ahead of time. That makes data entry easier and helps keep the data more consistent by preventing typos and slightly different names for the same thing.

| Cave | Entrance |
| --- | --- |
| Name | Coordinates |
| Alternate Names | Elevation |
| State | Location Quality (tag list; how the data was collected: GIS/Lidar, GPS point in the field, topo map, etc.) |
| County | Entrance Name |
| Length | Reported On |
| Depth | Reported By (tag list) |
| Max Pit Depth | Pit Depth |
| Number of Pits | Status (tag list) |
| Reported On | Field Indication (tag list) |
| Reported By (tag list) | Hydrology (tag list) |
| Geology (tag list) |  |
| Geologic Age (tag list) |  |
| Physiographic Province (tag list) |  |
| Biology (tag list) |  |
| Map Status (tag list) |  |
| Cartographers (tag list) |  |
| Narrative |  |
| Other Tags (tag list; for anything a survey wants to collect that is not already covered) |  |

### Files

Associate almost any type of file with a cave and view it in one place. Some examples are:

- Maps
- Sketch maps
- Articles
- Trip reports
- Photos
- Shapefiles and other spatial data
- Survey data
- Other cave-related files

## Search

- Search by any combination of the fields collected
- Run spatial searches by distance from a point or by finding only the caves within a polygon drawn on the map or by uploading a zipped shapefile
- Use the same filters on the map as the cave list
- Export search results as spreadsheet or GPX and choose which fields to include

For example, if someone is looking for a cave to visit or survey, they could search the narrative for words like `air`, `blowing`, `lead`, or `unexplored`, filter for caves that do not have a map and are estimated to be at least a few thousand feet long, then sort by distance from their location.

That gives them a useful starting point for finding caves that might have leads or unexplored areas. Doing that search across spreadsheets, files, or GIS tools would be more annoying. In Planarian, it is simple to perform.
## Cave Record

- View cave details, entrance details, and files right away in one place
- View Macrostrat geology data and the highest resolution geologic maps from the National Geologic Map Database for the area
- View whether the area is public land, which agency manages it, and nearby stream gages
- View line plots and other spatial file types

## Map

- View cave entrances and line plots on the map
- Use map layers like street, topo, satellite, lidar, public land, parcel boundaries, Macrostrat geology, NGMDB geology, hydrology, and watershed boundaries
- Click anywhere on the map to get information about that point, including coordinates, elevation, geology, and nearby stream gages
- The map stays fast even with tens of thousands of entrance points

## Admin

- Import cave and entrance CSVs with a dry run before changes are applied, then review inserts, updates, and deletes before processing
- Manage county codes, tags, and other account-level settings
- Create downloadable account archives and manage recent backups

## Permissions

Planarian is built for group-level data management, not just a personal database.

Planarian supports multiple survey accounts with separate permissions and settings. Users with access to more than one account can switch between them with the same login.

### Roles

- Admin: full access to the account and settings
- Manager: can edit and manage the caves they control and review submitted changes
- Viewer: can view data and suggest updates

### Permission Scope

- Broad account access
- State-level access
- County-level access
- Individual cave access

Users can be invited by managers or admins through the app, accept or decline pending invitations, and switch between survey accounts with the same login if they have access to more than one.

## Misc

- Favorite caves

## Developer Setup

- Frontend: React + TypeScript
- Backend: C#
- Database: PostgreSQL
- File storage: Azure Blob Storage or a local storage emulator

Planarian is not heavily dependent on Azure specifically. Most of the application can run anywhere that supports the backend, frontend, and PostgreSQL. The main Azure-specific piece right now is file storage, since files such as maps, articles, trip reports, and survey data are stored in blob storage. For local development, you can use a real Azure Blob Storage account or a local emulator like Azurite.

If you have any interest at all in working on Planarian, reach out to me. I am happy to help get you set up locally and figure out where to start.

If you want to work on the codebase locally:

1. Configure the development app settings for the backend.
2. Set up PostgreSQL.
3. Set up Azure Blob Storage for file uploads, or use a local emulator like Azurite.
4. Open `Planarian/Planarian.sln`.
5. Run the backend from Visual Studio or your normal .NET workflow.
6. Start the frontend from `Planarian.Web` with `npm start`.