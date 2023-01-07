import { isNullOrWhiteSpace } from "../../../Shared/Helpers/StringHelpers";
import { LeadClassification } from "../../Leads/Models/LeadClassification";

export const TherionService = {
  GetContinuationPoints(text: string): ContinuationPoint[] {
    const continuationPoints = readContinuationPoints(text);
    const stations = readStationPoints(text);

    // Find the closest station for each continuation point
    continuationPoints.forEach((continuationPoint) => {
      continuationPoint.closestStation = findClosestStation(
        continuationPoint,
        stations
      );
    });

    return continuationPoints;
  },
};

const readContinuationPoints = (text: string): ContinuationPoint[] => {
  const lines = text.split(/\r?\n/);
  const continuationPoints: ContinuationPoint[] = [];

  const regex = /point\s+(-?\d+.?\d*)\s+(-?\d+.?\d*)\s+(\w+)(.*)/;

  lines.forEach((line) => {
    const match = line.match(regex);
    if (match) {
      const x = Number(match[1]);
      const y = Number(match[2]);
      const pointType = match[3];

      if (pointType !== "continuation") {
        return;
      }

      const continuationPoint: ContinuationPoint = new ContinuationPoint(
        new Coordinates(x, y)
      );

      const args = match[4];

      // Extract argument name and value
      // Extract argument name and value
      const argRegex = /(-[a-zA-Z]+)\s+(?:"([^"]+)"|(\S+))/g;
      for (const argMatch of args.matchAll(argRegex)) {
        const argName = argMatch[1].replace(/^-+/, "");

        const argValue = argMatch[2].replace(/"+$/, "");

        if (argName === "text") {
          continuationPoint.description = argValue;
        }
      }

      continuationPoints.push(continuationPoint);
    }
  });

  return continuationPoints;
};

const readStationPoints = (text: string): Station[] => {
  const lines = text.split(/\r?\n/);
  const stations: Station[] = [];

  const regex = /point\s+(-?\d+.?\d*)\s+(-?\d+.?\d*)\s+(\w+)(.*)/;

  lines.forEach((line) => {
    const match = line.match(regex);
    if (match) {
      const x = Number(match[1]);
      const y = Number(match[2]);
      const pointType = match[3];

      if (pointType !== "station") {
        return;
      }

      const station: Station = new Station(new Coordinates(x, y));

      const args = match[4];

      // Extract argument name and value

      // Extract argument name and value
      const argRegex = /(-[a-zA-Z]+)\s+(?:""([^""]+)""|(\S+))/g;
      for (const argMatch of Array.from(args.matchAll(argRegex))) {
        const argName = argMatch[1].replace(/-/g, "");
        let argValue = argMatch[2]
          ? argMatch[2].replace(/^"|"$/g, "")
          : argMatch[3];

        argValue = argValue.replace(/^"|"$/g, ""); // remove leading and trailing quotes

        if (argName === "name") {
          station.name = argValue;
        }
      }

      stations.push(station);
    }
  });

  return stations;
};

const findClosestStation = (
  continuationPoint: ContinuationPoint,
  stations: Station[]
): Station | undefined => {
  let closestStation: Station | undefined = undefined;
  let minDistance = Number.MAX_VALUE;

  stations.forEach((station) => {
    const distance = Math.sqrt(
      Math.pow(station.coordinates.x - continuationPoint.coordinates.x, 2) +
        Math.pow(station.coordinates.y - continuationPoint.coordinates.y, 2)
    );
    if (distance < minDistance) {
      closestStation = station;
      minDistance = distance;
    }
  });

  return closestStation;
};

export class ContinuationPoint {
  constructor(coordinates: Coordinates) {
    this.coordinates = coordinates;
  }

  public get classification(): LeadClassification {
    if (isNullOrWhiteSpace(this.description)) return LeadClassification.UNKNOWN;

    // Define the regular expressions for "good," "decent," and "bad" leads
    const goodRegex =
      /\b(goes|airflow|stooping|stoop|amazing|borehole|exemplary|excellent|fabulous|fine|formations|first-rate|good|great|helectite|outstanding|splendid|stellar|superb|superlative|top-notch|walking|walk|wind|wonderful)\b/;
    const decentRegex =
      /\b(hands|knee|hands and knees|small|acceptable|adequate|crawl|crawl to crawl|decent|fair|fair to middling|might open up|moderate|not bad|passable|reasonable|satisfactory|so-so|tolerable|could lead to something)\b/;
    const badRegex =
      /\b(doesn't go|does not go|loop|loops|terrible|pinches|pinch|abysmal|appalling|atrocious|awful|bad|dismal|disappointing|dig|execrable|lamentable|miserable|ngl|n\^[0-9]+|pitiful|poor|squeeze|terrible|tiny|very bad)\b/;

    const leadDescription = this.description?.toLowerCase() ?? "";
    // Count the number of "good," "decent," and "bad" words in the lead description
    const goodWordCount = (leadDescription.match(goodRegex) || []).length;
    const decentWordCount = (leadDescription.match(decentRegex) || []).length;
    const badWordCount = (leadDescription.match(badRegex) || []).length;

    // Determine whether the lead is good, decent, or bad based on the word counts
    if (goodWordCount > decentWordCount && goodWordCount > badWordCount) {
      return LeadClassification.GOOD;
    }

    if (decentWordCount > goodWordCount && decentWordCount > badWordCount) {
      return LeadClassification.DECENT;
    }

    if (badWordCount > goodWordCount && badWordCount > decentWordCount) {
      return LeadClassification.BAD;
    }

    return LeadClassification.UNKNOWN;
  }

  public coordinates: Coordinates;
  public description?: string;
  public closestStation?: Station;
}

export class Coordinates {
  x: number;
  y: number;

  constructor(x: number, y: number) {
    this.x = x;
    this.y = y;
  }
}

export class Station {
  constructor(coordinates: Coordinates) {
    this.coordinates = coordinates;
  }
  coordinates: Coordinates;
  name?: string;
}
