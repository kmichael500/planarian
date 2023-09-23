import { HttpClient } from "../..";

const baseUrl = "api/app";
let AppOptions: AppInitializeVm;
const AppService = {
  async InitializeApp(): Promise<void> {
    const response = await HttpClient.get<AppInitializeVm>(
      `${baseUrl}/initialize`
    );
    AppOptions = response.data;
  },
};
export { AppService, AppOptions };

export interface AppInitializeVm {
  serverBaseUrl: string;
  signalrBaseUrl: string;
}
