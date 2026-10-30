import dayjs from "dayjs";
import { EditFileMetadataVm } from "../../Files/Models/EditFileMetadataVm";
import { FileTypeKey } from "../../Files/Models/FileTypeKey";
import { AddCaveVm } from "../Models/AddCaveVm";
import { AddEntranceVm } from "../Models/AddEntranceVm";
import { GeoJsonUploadVm } from "../Models/GeoJsonUploadVm";
import { CaveSnapshotVm, SnapshotTagReference } from "../Models/CaveRevisionVm";
import { CaveVm } from "../Models/CaveVm";
import { caveToForm, snapshotToForm } from "./CaveFormMapper";

// Compile-time drift guards. These are intentionally explicit: adding a new authoring property must make
// somebody decide how it is loaded, preserved, cleared, and submitted before the frontend can build.
const caveAuthoringFields: Record<keyof AddCaveVm, true> = {
  id: true,
  expectedRevisionId: true,
  name: true,
  alternateNames: true,
  countyId: true,
  stateId: true,
  countyDisplayId: true,
  countyNumber: true,
  isCountyNumberManuallySet: true,
  useFirstAvailableCountyNumber: true,
  lengthFeet: true,
  depthFeet: true,
  maxPitDepthFeet: true,
  numberOfPits: true,
  narrative: true,
  reportedOn: true,
  entrances: true,
  geologyTagIds: true,
  files: true,
  linePlots: true,
  reportedByNameTagIds: true,
  biologyTagIds: true,
  archeologyTagIds: true,
  cartographerNameTagIds: true,
  mapStatusTagIds: true,
  geologicAgeTagIds: true,
  physiographicProvinceTagIds: true,
  otherTagIds: true,
};

const entranceAuthoringFields: Record<keyof AddEntranceVm, true> = {
  id: true,
  isPrimary: true,
  locationQualityTagId: true,
  name: true,
  description: true,
  latitude: true,
  longitude: true,
  elevationFeet: true,
  reportedOn: true,
  pitFeet: true,
  entranceStatusTagIds: true,
  fieldIndicationTagIds: true,
  entranceHydrologyTagIds: true,
  entranceOtherTagIds: true,
  reportedByNameTagIds: true,
};

const fileAuthoringFields: Record<keyof EditFileMetadataVm, true> = {
  id: true,
  name: true,
  extension: true,
  fileTypeTagId: true,
  fileTypeKey: true,
};

const linePlotAuthoringFields: Record<keyof GeoJsonUploadVm, true> = {
  id: true,
  geoJson: true,
  name: true,
};

it("maps every live Cave field that can be authored into editor state", () => {
  const cave: CaveVm = {
    id: "cave000001",
    currentRevisionId: "revision01",
    displayId: "CNT-42",
    countyId: "county0001",
    stateId: "state00001",
    countyDisplayId: "CNT",
    countyIdDelimiter: "-",
    countyNumber: 42,
    name: "Mapped Cave",
    alternateNames: ["Alias A", "Alias B"],
    lengthFeet: 123.5,
    depthFeet: 54.25,
    maxPitDepthFeet: 31.75,
    numberOfPits: 4,
    narrative: "Narrative",
    reportedOn: "2026-01-02T00:00:00Z",
    isArchived: false,
    primaryEntrance: null,
    mapIds: ["legacy-map-id"],
    entrances: [{
      id: "entrance01",
      isPrimary: true,
      locationQualityTagId: "quality001",
      name: "Main Entrance",
      description: "Description",
      latitude: 35.1234567,
      longitude: -86.7654321,
      elevationFeet: 612.25,
      reportedOn: "2026-02-03T00:00:00Z",
      pitFeet: 88.5,
      entranceStatusTagIds: ["status001"],
      fieldIndicationTagIds: ["field001"],
      entranceHydrologyTagIds: ["hydro001"],
      entranceOtherTagIds: ["entrance-other"],
      reportedByNameTagIds: ["entrance-person"],
    }],
    geologyTagIds: ["geology001"],
    files: [{
      id: "file000001",
      name: "Survey",
      extension: ".pdf",
      fileTypeTagId: "filetype01",
      fileTypeKey: FileTypeKey.Map,
    }],
    reportedByNameTagIds: ["cave-person"],
    biologyTagIds: ["biology001"],
    archeologyTagIds: ["archeology1"],
    cartographerNameTagIds: ["cartographer"],
    mapStatusTagIds: ["mapstatus1"],
    geologicAgeTagIds: ["geoage001"],
    physiographicProvinceTagIds: ["physio001"],
    otherTagIds: ["cave-other"],
  };
  const linePlots: GeoJsonUploadVm[] = [{
    id: "lineplot01",
    name: "Main Traverse",
    geoJson: "{\"type\":\"FeatureCollection\",\"features\":[]}",
  }];

  const mapped = caveToForm(cave, linePlots);

  expect({
    ...mapped,
    reportedOn: mapped.reportedOn?.toISOString(),
    entrances: mapped.entrances.map(entrance => ({
      ...entrance,
      reportedOn: entrance.reportedOn?.toISOString(),
    })),
  }).toEqual({
    id: "cave000001",
    expectedRevisionId: "revision01",
    name: "Mapped Cave",
    alternateNames: ["Alias A", "Alias B"],
    countyId: "county0001",
    stateId: "state00001",
    countyDisplayId: "CNT",
    countyNumber: 42,
    isCountyNumberManuallySet: false,
    useFirstAvailableCountyNumber: false,
    lengthFeet: 123.5,
    depthFeet: 54.25,
    maxPitDepthFeet: 31.75,
    numberOfPits: 4,
    narrative: "Narrative",
    reportedOn: "2026-01-02T00:00:00.000Z",
    entrances: [{
      id: "entrance01",
      isPrimary: true,
      locationQualityTagId: "quality001",
      name: "Main Entrance",
      description: "Description",
      latitude: 35.1234567,
      longitude: -86.7654321,
      elevationFeet: 612.25,
      reportedOn: "2026-02-03T00:00:00.000Z",
      pitFeet: 88.5,
      entranceStatusTagIds: ["status001"],
      fieldIndicationTagIds: ["field001"],
      entranceHydrologyTagIds: ["hydro001"],
      entranceOtherTagIds: ["entrance-other"],
      reportedByNameTagIds: ["entrance-person"],
    }],
    geologyTagIds: ["geology001"],
    files: [{
      id: "file000001",
      name: "Survey",
      extension: ".pdf",
      fileTypeTagId: "filetype01",
      fileTypeKey: FileTypeKey.Map,
    }],
    linePlots,
    reportedByNameTagIds: ["cave-person"],
    biologyTagIds: ["biology001"],
    archeologyTagIds: ["archeology1"],
    cartographerNameTagIds: ["cartographer"],
    mapStatusTagIds: ["mapstatus1"],
    geologicAgeTagIds: ["geoage001"],
    physiographicProvinceTagIds: ["physio001"],
    otherTagIds: ["cave-other"],
  });
});

const tag = (role: string, tagTypeId: string): SnapshotTagReference => ({
  role,
  tagTypeId,
  nameAtRevision: tagTypeId,
});

it("maps every proposal snapshot field and tag role into revisable editor state", () => {
  const snapshot: CaveSnapshotVm = {
    caveId: "cave000001",
    accountId: "account001",
    name: "Proposal Cave",
    alternateNames: ["Proposal Alias"],
    state: { id: "state00001", nameAtRevision: "State", abbreviationAtRevision: "ST" },
    county: { id: "county0001", nameAtRevision: "County", displayIdAtRevision: "CNT" },
    countyNumber: 42,
    lengthFeet: 201.5,
    depthFeet: 91.25,
    maxPitDepthFeet: 40.5,
    numberOfPits: 7,
    narrative: "Proposal narrative",
    reportedOn: "2026-03-04T00:00:00Z",
    isArchived: false,
    tags: [
      tag("Geology", "geology001"),
      tag("GeologicAge", "geoage001"),
      tag("MapStatus", "mapstatus1"),
      tag("PhysiographicProvince", "physio001"),
      tag("Archeology", "archeology1"),
      tag("Biology", "biology001"),
      tag("CaveOther", "cave-other"),
      tag("Cartographer", "cartographer"),
      tag("CaveReportedBy", "cave-person"),
    ],
    entrances: [{
      id: "entrance01",
      isPrimary: true,
      name: "Snapshot Entrance",
      description: "Snapshot description",
      latitude: 35.5,
      longitude: -86.5,
      elevation: 700.25,
      srid: 4326,
      locationQualityTagId: "quality001",
      locationQualityNameAtRevision: "Survey Grade",
      reportedOn: "2026-04-05T00:00:00Z",
      pitDepthFeet: 90.5,
      tags: [
        tag("EntranceStatus", "status001"),
        tag("FieldIndication", "field001"),
        tag("EntranceHydrology", "hydro001"),
        tag("EntranceOther", "entrance-other"),
        tag("EntranceReportedBy", "entrance-person"),
      ],
    }],
    files: [{
      id: "file000001",
      fileTypeTagId: "filetype01",
      fileTypeNameAtRevision: "Map",
      name: "Published Map",
      extension: ".pdf",
    }],
    linePlots: [{ id: "lineplot01", name: "Snapshot Traverse", contentHash: "hash" }],
  };
  const stagedFile = {
    id: "file000002",
    fileTypeTagId: "filetype02",
    fileTypeNameAtRevision: "Document",
    name: "Staged Document",
    extension: ".txt",
  };
  const linePlots: GeoJsonUploadVm[] = [{
    id: "lineplot01",
    name: "Snapshot Traverse",
    geoJson: "{\"type\":\"FeatureCollection\",\"features\":[]}",
  }];

  const mapped = snapshotToForm(snapshot, "Manual", 77, [stagedFile], linePlots);

  expect({
    ...mapped,
    reportedOn: mapped.reportedOn?.toISOString(),
    entrances: mapped.entrances.map(entrance => ({
      ...entrance,
      reportedOn: entrance.reportedOn?.toISOString(),
    })),
  }).toEqual({
    id: "cave000001",
    name: "Proposal Cave",
    alternateNames: ["Proposal Alias"],
    countyId: "county0001",
    stateId: "state00001",
    countyDisplayId: "CNT",
    countyNumber: 77,
    isCountyNumberManuallySet: true,
    useFirstAvailableCountyNumber: false,
    lengthFeet: 201.5,
    depthFeet: 91.25,
    maxPitDepthFeet: 40.5,
    numberOfPits: 7,
    narrative: "Proposal narrative",
    reportedOn: "2026-03-04T00:00:00.000Z",
    entrances: [{
      id: "entrance01",
      isPrimary: true,
      locationQualityTagId: "quality001",
      name: "Snapshot Entrance",
      description: "Snapshot description",
      latitude: 35.5,
      longitude: -86.5,
      elevationFeet: 700.25,
      reportedOn: "2026-04-05T00:00:00.000Z",
      pitFeet: 90.5,
      entranceStatusTagIds: ["status001"],
      fieldIndicationTagIds: ["field001"],
      entranceHydrologyTagIds: ["hydro001"],
      entranceOtherTagIds: ["entrance-other"],
      reportedByNameTagIds: ["entrance-person"],
    }],
    geologyTagIds: ["geology001"],
    files: [{
      id: "file000001",
      fileTypeTagId: "filetype01",
      name: "Published Map",
      extension: ".pdf",
      fileTypeKey: FileTypeKey.Map,
    }, {
      id: "file000002",
      fileTypeTagId: "filetype02",
      name: "Staged Document",
      extension: ".txt",
      fileTypeKey: "Document",
    }],
    linePlots,
    reportedByNameTagIds: ["cave-person"],
    biologyTagIds: ["biology001"],
    archeologyTagIds: ["archeology1"],
    cartographerNameTagIds: ["cartographer"],
    mapStatusTagIds: ["mapstatus1"],
    geologicAgeTagIds: ["geoage001"],
    physiographicProvinceTagIds: ["physio001"],
    otherTagIds: ["cave-other"],
  });

  expect(dayjs.isDayjs(mapped.reportedOn)).toBe(true);
  expect(dayjs.isDayjs(mapped.entrances[0].reportedOn)).toBe(true);
});
