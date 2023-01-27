import { Coordinates } from "./Coordinates";

export class Station {
  coordinates: Coordinates;
  name?: string;

  constructor(coordinates: Coordinates) {
    this.coordinates = coordinates;
  }
}
