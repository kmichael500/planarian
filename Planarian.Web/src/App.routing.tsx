import { Navigate, Route, Routes } from "react-router-dom";
import { ProtectedRoutesComponent } from "./Modules/Authentication/Components/ProtectedRoutesComponent";
import { ConfirmEmailPage } from "./Modules/Authentication/Pages/ConfirmEmailPage";
import { LoginPage } from "./Modules/Authentication/Pages/LoginPage";
import { ResetPasswordPage } from "./Modules/Authentication/Pages/ResetPasswordPage";
import { RegisterPage } from "./Modules/Authentication/Register/Components/RegisterPage";
import { ProjectPage } from "./Modules/Project/Pages/ProjectPage";
import { ProjectsPage } from "./Modules/Project/Pages/ProjectsPage";
import { SettingsPage } from "./Modules/Setting/Pages/SettingsPage";
import { LeadAddPage } from "./Modules/Trip/Pages/LeadAddPage";
import { TripPage } from "./Modules/Trip/Pages/TripPage";
import { TripPhotoUploadPage } from "./Modules/Trip/Pages/TripPhotoUploadPage";
import { CavesPage } from "./Modules/Caves/Pages/CavesPage";
import { AddCavesPage } from "./Modules/Caves/Pages/AddCavePage";

export const AppRouting: React.FC = () => {
  return (
    <Routes>
      <Route path="/login" element={<LoginPage />}></Route>
      <Route path="/register" element={<RegisterPage />}></Route>
      <Route path="/reset-password" element={<ResetPasswordPage />}></Route>
      <Route path="/confirm-email" element={<ConfirmEmailPage />}></Route>
      <Route element={<ProtectedRoutesComponent />}>
        <Route path="/caves" element={<CavesPage />} />
        <Route path="/caves/add" element={<AddCavesPage />} />
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
        <Route path="/settings" element={<SettingsPage />}></Route>
        <Route path="*" element={<Navigate to="/projects" replace />} />
      </Route>
    </Routes>
  );
};

export interface HasRoute {
  route: string;
}
