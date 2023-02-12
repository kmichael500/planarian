import { HttpClient } from "../../..";
import { CreateOrEditProject } from "../Models/CreateOrEditProject";
import { ProjectVm } from "../Models/ProjectVm";
import { SelectListItem } from "../../../Shared/Models/SelectListItem";
import { InviteMember } from "../../../Shared/Models/InviteMember";
import { TripVm } from "../../Trip/Models/TripVm";
import { CreateOrEditTripVm } from "../../Trip/Models/CreateOrEditTripVm";

const baseUrl = "api/projects";
const ProjectService = {
  //#region Project
  /**
   *
   * @param project
   * @returns Id of the project.
   */
  async CreateProject(project: CreateOrEditProject): Promise<ProjectVm> {
    const response = await HttpClient.post<ProjectVm>(`${baseUrl}/`, project);
    return response.data;
  },

  /**
   *
   * @returns List of projects that user has access to.
   */
  async GetProjects(): Promise<ProjectVm[]> {
    const response = await HttpClient.get<ProjectVm[]>(`${baseUrl}/`);
    return response.data;
  },

  /**
   *
   * @param projectId
   * @returns a project
   */
  async GetProject(projectId: string): Promise<ProjectVm> {
    const response = await HttpClient.get<ProjectVm>(`${baseUrl}/${projectId}`);
    return response.data;
  },

  //#endregion

  //#region Trip

  async AddTrip(values: CreateOrEditTripVm): Promise<TripVm> {
    const response = await HttpClient.post<TripVm>(
      `${baseUrl}/${values.projectId}/trips`,
      values
    );
    return response.data;
  },

  async GetTrips(projectId: string): Promise<TripVm[]> {
    const response = (await HttpClient.get<TripVm[]>(
      `${baseUrl}/${projectId}/trips`
    )) as any;
    return response.data.results;
  },

  //#endregion

  //#region Project Members
  async GetProjectMembers(
    projectId: string
  ): Promise<SelectListItem<string>[]> {
    const response = await HttpClient.get<SelectListItem<string>[]>(
      `${baseUrl}/${projectId}/members`
    );
    return response.data;
  },

  async AddProjectMember(
    userId: string,
    projectId: string
  ): Promise<SelectListItem<string>[]> {
    const response = await HttpClient.post<SelectListItem<string>[]>(
      `${baseUrl}/${projectId}/members/${userId}`
    );
    return response.data;
  },
  async AddProjectMembers(
    userIds: string[],
    projectId: string
  ): Promise<SelectListItem<string>[]> {
    const response = await HttpClient.post<SelectListItem<string>[]>(
      `${baseUrl}/${projectId}/members`,
      userIds
    );
    return response.data;
  },

  async DeleteProjectMember(
    userId: string,
    projectId: string
  ): Promise<SelectListItem<string>[]> {
    const response = await HttpClient.delete<SelectListItem<string>[]>(
      `${baseUrl}/${projectId}/members/${userId}`
    );
    return response.data;
  },

  //#endregion

  //#region

  async InviteProjectMembers(
    values: InviteMember,
    projectId: string
  ): Promise<void> {
    const response = await HttpClient.post<void>(
      `${baseUrl}/${projectId}/members/invite`,
      values
    );
    return response.data;
  },
  //#endregion
};

export { ProjectService };
