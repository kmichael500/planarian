export const downloadCSV = (data: any[], ignoreId: boolean = false): void => {
  let csvContent = "data:text/csv;charset=utf-8,";
  let keys = Object.keys(data[0]);
  let headers = keys
    .filter((key) => key !== "id" || !ignoreId)
    .map((key) => {
      if (key.match(/[A-Z]/)) {
        return key.replace(/([A-Z])/g, " $1").replace(/^./, function (str) {
          return str.toUpperCase();
        });
      }
      return key.charAt(0).toUpperCase() + key.slice(1);
    });
  csvContent += headers.join(",") + "\n";
  data.forEach((item) => {
    let row = keys
      .filter((key) => key !== "id" || !ignoreId)
      .map((key) => {
        let value = item[key];
        if (typeof value === "string") {
          value = value.replace(/"/g, '""');
          value = value.replace(/\r?\n|\r/g, " ");
          value = value.replace(/\\/g, "\\\\");
          value = `"${value}"`;
        }
        return value;
      })
      .join(",");
    csvContent += row + "\n";
  });
  let encodedUri = encodeURI(csvContent);
  let link = document.createElement("a");
  link.setAttribute("href", encodedUri);
  link.setAttribute("download", "data.csv");
  document.body.appendChild(link);
  link.click();
};
