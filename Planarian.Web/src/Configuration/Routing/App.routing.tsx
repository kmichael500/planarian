import { Routes, Route, Navigate } from "react-router-dom";
import { AccountSettingsPage } from "../../Modules/Account/Pages/AccountSettingsPage";
import { ProtectedRoutesComponent } from "../../Modules/Authentication/Components/ProtectedRoutesComponent";
import { ConfirmEmailPage } from "../../Modules/Authentication/Pages/ConfirmEmailPage";
import { LoginPage } from "../../Modules/Authentication/Pages/LoginPage";
import { ResetPasswordPage } from "../../Modules/Authentication/Pages/ResetPasswordPage";
import { RegisterPage } from "../../Modules/Authentication/Register/Components/RegisterPage";
import { AddCavesPage } from "../../Modules/Caves/Pages/AddCavePage";
import { CavePage } from "../../Modules/Caves/Pages/CavePage";
import { CavesPage } from "../../Modules/Caves/Pages/CavesPage";
import { EditCavePage } from "../../Modules/Caves/Pages/EditCavePage";
import { ImportPage } from "../../Modules/Import/Pages/ImportPage";
import { ProjectPage } from "../../Modules/Project/Pages/ProjectPage";
import { ProjectsPage } from "../../Modules/Project/Pages/ProjectsPage";
import { ProfilePage } from "../../Modules/Setting/Pages/SettingsPage";
import { LeadAddPage } from "../../Modules/Trip/Pages/LeadAddPage";
import { TripPage } from "../../Modules/Trip/Pages/TripPage";
import { TripPhotoUploadPage } from "../../Modules/Trip/Pages/TripPhotoUploadPage";
import { NotFoundPage } from "../../Shared/Pages/NotFoundPage";
import { UnauthorizedPage } from "../../Shared/Pages/Unauthorized";
import { AppRederect } from "./App.routing.redirect";
import { MapPage } from "../../Modules/Map/Pages/MapPage";
import { UserManagerPage } from "../../Modules/Account/Pages/UserManagerPage";

export const AppRouting: React.FC = () => {
  return (
    <Routes>
      <Route path="/login" element={<LoginPage />}></Route>
      <Route path="/register" element={<RegisterPage />}></Route>
      <Route path="/reset-password" element={<ResetPasswordPage />}></Route>
      <Route path="/confirm-email" element={<ConfirmEmailPage />}></Route>
      <Route path="/not-found" element={<NotFoundPage />}></Route>
      <Route path="/unauthorized" element={<UnauthorizedPage />}></Route>

      <Route element={<ProtectedRoutesComponent />}>
        <Route path="/" element={<AppRederect />} />
        <Route path="/caves" element={<CavesPage />} />
        <Route path="/caves/:caveId" element={<CavePage />} />
        <Route path="/caves/:caveId/edit" element={<EditCavePage />} />
        <Route path="/caves/add" element={<AddCavesPage />} />
        <Route path="/map" element={<MapPage />} />
        <Route path="/account/settings" element={<AccountSettingsPage />} />
        <Route path="/account/users" element={<UserManagerPage />} />
        <Route path="/account/import" element={<ImportPage />} />
        <Route path="/projects" element={<ProjectsPage />}></Route>
        <Route path="/projects/:projectId" element={<ProjectPage />}></Route>
        <Route
          path="/projects/:projectId/trip/:tripId"
          element={<TripPage />}
        ></Route>
        <Route
          path="/projects/:projectId/trip/:tripId/uploadPhotos"
          element={<TripPhotoUploadPage />}
        ></Route>
        <Route
          path="/projects/:projectId/trip/:tripId/addLeads"
          element={<LeadAddPage />}
        ></Route>
        <Route path="/profile" element={<ProfilePage />}></Route>
        <Route path="*" element={<Navigate to="/not-found" replace />} />
      </Route>
      <Route path="*" element={<Navigate to="/not-found" replace />} />
    </Routes>
  );
};

export interface HasRoute {
  route: string;
}
