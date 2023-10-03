class NotFoundException extends Error {}

export { NotFoundException };

export class PlanarianError extends Error {
  constructor(message: string) {
    super(message);
    this.name = "PlanarianError";
  }
}

export class NotFouneError extends PlanarianError {
  constructor(parameter: string) {
    super(`Not found ${parameter}`);
    this.name = "NotFoundError";
  }
}
