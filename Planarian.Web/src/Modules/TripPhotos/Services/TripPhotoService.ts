import { HttpClient } from "../../..";

const baseUrl = "api/photos";
const TripPhotoService = {
  async DeleteTripPhoto(id: string): Promise<void> {
    await HttpClient.delete(`${baseUrl}/${id}`);
  },
};
export { TripPhotoService };
