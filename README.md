# Planarian

Planarian is a cave data platform that lets a state cave survey easily collect, manage, and explore cave information in one place. It is built for non-GIS users while still supporting advanced spatial search and mapping.

## Features

- One source of truth for caves, entrances, and related files  
- Search by attributes and spatial filters  
- Interactive map with common base layers and 3D terrain  
- Granular permissions by role and geography  
- Export search results as CSV or GPX  
- In the Works: approval workflow with edit history

## Why

Cave records are often split across spreadsheets, databases, shared drives, and GIS tools. Planarian centralizes the data, adds access control, and makes search and mapping usable for everyone.

## Data model

**Cave fields**  
Name, Alternate Names, State, County, Length, Depth, Max Pit Depth, Number of Pits, Reported On, Reported By, Geology, Geologic Age, Physiographic Province, Biology, Map Status, Cartographers, Narrative, Other.

**Entrance fields**  
Coordinates, Elevation, Location Quality, Name, Reported On, Reported By, Pit Depth, Status (for example private property), Field Indication, Hydrology, Land Access.

**Files**  
Maps, Articles, Trip Reports, Photos, Shapefiles, Survey Data, and more.

## Data collection

- Limited required fields so contributors can submit what they know  
- Anyone can propose changes  
- Predefined lists to reduce typos  
- All changes go through review before publish

## Search

- Filter by any combination of fields  
- Spatial filters: distance from a point, polygon search, shapefile queries  
- Sort by distance, length, depth, and more  
- Export results as CSV or GPX

## Map

- Simple controls for non-GIS users  
- Layers: Street, Topo, Satellite, Lidar, Public Land, Macrostrat Geology, NGBD Geology, Hydrology, Watershed boundaries  
- Overlay shapefiles  
- Optional 3D terrain

## Cave page

- All cave details on one page  
- Links to local geology via Macrostrat and the NGMDB Mapviewer  
- Nearest stream gage height

## Users and permissions

**Roles**  
- Admin - full access to the system and can manage all data, permissions, and account settings  
- Manager - responsible for reviewing and approving changes. They can also invite other members within the state survey to view or manage the data they control  
- Viewer - can view cave data and suggest updates. Their changes will need to be approved by a manager or an admin before any changes are published

**Permission Scope**  
- All locations  
- Specific states and counties  
- Individual caves
- Future: within a polygon on a map
- Future: permission groups to quickly assign people to a set of caves (i.e. highly sensitive, moderately sensitive, not sensitive)

## Settings

- Manage tags definitions (i.e geology, map status, file types, etc)  
- Disable certain fields
- Manage counties
- Configure defaults such as export enabled
