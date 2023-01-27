import { isNullOrWhiteSpace } from "../../../Shared/Helpers/StringHelpers";
import { LeadClassification } from "../../Leads/Models/LeadClassification";
import { Station } from "./Station";
import { Coordinates } from "./Coordinates";

export class ContinuationPoint {
  public coordinates: Coordinates;
  public description?: string;
  public closestStation?: Station;

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
}
