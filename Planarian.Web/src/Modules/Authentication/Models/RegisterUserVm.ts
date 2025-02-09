export interface RegisterUserVm {
  firstName: string;
  lastName: string;
  emailAddress: string;
  password: string;
  phoneNumber: string;

  invitationCode?: string;
}
