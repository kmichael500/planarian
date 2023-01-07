import { serialize } from "object-to-formdata";

export const HttpHelpers = {
  ToFormData(data: any): FormData {
    var formData = serialize(data);
    console.log(formData);
    return formData;
  },
};
