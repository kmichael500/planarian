import { SelectListItem } from "./SelectListItem";

export interface RevokedAccountVm extends SelectListItem<string> {
  reason?: string | null;
}
