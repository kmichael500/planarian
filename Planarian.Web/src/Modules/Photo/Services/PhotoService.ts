import { HttpClient } from "../../..";

const baseUrl = "api/photos";
const PhotoService = {
  async DeleteTripPhoto(id: string): Promise<void> {
    await HttpClient.delete(`${baseUrl}/${id}`);
  },
};
export { PhotoService };
