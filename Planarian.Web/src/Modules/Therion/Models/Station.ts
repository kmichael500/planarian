import { Coordinates } from "./Coordinates";

export class Station {
  constructor(coordinates: Coordinates) {
    this.coordinates = coordinates;
  }
  coordinates: Coordinates;
  name?: string;
}
