import { HttpClient } from "../../..";

const baseUrl = "api/tripPhotos";
const TripPhotoService = {
  async DeleteTripPhoto(id: string): Promise<void> {
    await HttpClient.delete(`${baseUrl}/${id}`);
  },
};
export { TripPhotoService };
