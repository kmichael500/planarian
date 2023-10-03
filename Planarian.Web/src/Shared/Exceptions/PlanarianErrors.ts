export class PlanarianError extends Error {
  constructor(message: string) {
    super(message);
    this.name = "PlanarianError";
  }
}

export class NotFoundError extends PlanarianError {
  constructor(parameter: string) {
    super(`Not found ${parameter}`);
    this.name = "NotFoundError";
  }
}
