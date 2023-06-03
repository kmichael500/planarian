interface QueryParams {
  [key: string]: string;
}

export class QueryStringParser {
  // private static parseQueryParamValue(
  //   value: string
  // ): string | number | boolean {
  //   if (value === "true") {
  //     return true;
  //   }
  //   if (value === "false") {
  //     return false;
  //   }
  //   const parsedValue = Number(value);
  //   return isNaN(parsedValue) ? value : parsedValue;
  // }

  // private static parseQueryParamKey(key: string): string | QueryParams {
  //   const match = key.match(/^([^\[\]]+)(\[\])?/);
  //   if (match) {
  //     const name = match[1];
  //     const isArray = !!match[2];
  //     return isArray ? { [name]: [] } : name;
  //   }
  //   return "";
  // }

  // static parse<T>(queryString: string): T {
  //   const queryParams = new URLSearchParams(queryString);
  //   const parsedQuery: QueryParams = {};

  //   queryParams.forEach((value, key) => {
  //     const parsedValue = this.parseQueryParamValue(value);
  //     const parsedKey = this.parseQueryParamKey(key);
  //     if (typeof parsedKey === "string") {
  //       parsedQuery[parsedKey] = parsedValue;
  //     } else {
  //       const arrayName = Object.keys(parsedKey)[0];
  //       const index = Number(key.match(/^\w+\[(\d+)\]/)[1]);
  //       parsedQuery[arrayName][index] = parsedValue;
  //     }
  //   });

  //   return parsedQuery as unknown as T;
  // }

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
