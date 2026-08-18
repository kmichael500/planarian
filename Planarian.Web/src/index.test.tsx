export {};

type PromiseConstructorWithResolvers = PromiseConstructor & {
  withResolvers?: <T>() => {
    promise: Promise<T>;
    resolve: (value: T | PromiseLike<T>) => void;
    reject: (reason?: unknown) => void;
  };
};

jest.mock("react-dom/client", () => ({
  __esModule: true,
  default: { createRoot: () => ({ render: jest.fn() }) },
}));

jest.mock("@syncfusion/ej2-base", () => ({
  registerLicense: jest.fn(),
}));

jest.mock("./App", () => ({
  __esModule: true,
  default: () => null,
}));

jest.mock("./reportWebVitals", () => ({
  __esModule: true,
  default: jest.fn(),
}));
describe("browser authentication migration", () => {
  beforeEach(() => {
    localStorage.clear();
    document.body.innerHTML = '<div id="root"></div>';
  });

  it("removes the legacy bearer token from localStorage during bootstrap", () => {
    localStorage.setItem("token", "legacy-bearer-token");

    jest.isolateModules(() => {
      require("./index");
    });

    expect(localStorage.getItem("token")).toBeNull();
  });

  it("installs Promise.withResolvers compatibility support during bootstrap", () => {
    const promiseConstructor = Promise as PromiseConstructorWithResolvers;
    const original = Object.getOwnPropertyDescriptor(promiseConstructor, "withResolvers");
    delete promiseConstructor.withResolvers;

    try {
      jest.isolateModules(() => {
        require("./index");
      });

      expect(typeof promiseConstructor.withResolvers).toBe("function");
    } finally {
      if (original) Object.defineProperty(promiseConstructor, "withResolvers", original);
      else delete promiseConstructor.withResolvers;
    }
  });
});
