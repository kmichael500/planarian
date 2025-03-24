import { NavigateFunction } from "react-router-dom";
import { isNullOrWhiteSpace } from "../Helpers/StringHelpers";

const NavigationService = {
  NavigateToMap(
    latitude: number,
    longittude: number,
    zoom: number,
    navigate: NavigateFunction
  ) {
    const url = NavigationService.GenerateMapUrl(latitude, longittude, zoom);
    navigate(url);
  },
  GenerateMapUrl(latitude?: number, longittude?: number, zoom?: number) {
    const searchParams = new URLSearchParams();
    if (latitude) {
      searchParams.set("lat", latitude.toString());
    }
    if (longittude) {
      searchParams.set("lng", longittude.toString());
    }
    if (zoom) {
      searchParams.set("zoom", zoom.toString());
    }
    const queryString = searchParams.toString();
    return `/map${!isNullOrWhiteSpace(queryString) ? `?${queryString}` : ""}`;
  },
  GenerateCaveUrl(caveId: string) {
    return `/caves/${caveId}`;
  },
  NavigateToCave(caveId: string, navigate: NavigateFunction) {
    const url = NavigationService.GenerateCaveUrl(caveId);
    navigate(url);
  },
};
export { NavigationService };
