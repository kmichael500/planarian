import { LoginCredentialsVm } from "./LoginCredentialsVm";

export interface BrowserLoginVm extends LoginCredentialsVm {
  remember?: boolean;
  invitationCode?: string;
}
