
export class QueryStringParser {
  static parseQueryString<T>(queryString: string) {
    const urlParams = new URLSearchParams(queryString);
    const queryObject = {} as any;

    for (const [key, value] of urlParams.entries()) {
      const [prefix, index, suffix] = key.split(/[\[\].]+/);

      if (prefix && index) {
        const i = parseInt(index, 10);
        queryObject[prefix] = queryObject[prefix] || [];
        queryObject[prefix][i] = queryObject[prefix][i] || {};
        queryObject[prefix][i][suffix] = decodeURIComponent(value);
      } else {
        queryObject[key] = decodeURIComponent(value);
      }

      // recursively remove the '.' prefix from object properties
      const obj = queryObject[key];
      if (typeof obj === "object" && obj !== null) {
        for (const prop in obj) {
          if (prop.startsWith(".")) {
            const value = obj[prop];
            delete obj[prop];
            obj[prop.substring(1)] = value;
          }
        }
      }
    }

    return queryObject as unknown as T;
  }
}
