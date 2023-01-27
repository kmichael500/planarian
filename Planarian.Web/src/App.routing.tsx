import {
  BrowserRouter as Router,
  Routes,
  Route,
  Link,
  Navigate,
} from "react-router-dom";
import { ConfirmEmailComponent } from "./Modules/Authentication/Components/ConfirmEmailComponent";
import { LoginComponent } from "./Modules/Authentication/Components/LoginComponent";
import { ProtectedRoutesComponent } from "./Modules/Authentication/Components/ProtectedRoutesComponent";
import { ResetPasswordComponent } from "./Modules/Authentication/Components/ResetPasswordComponent";
import { RegisterComponent } from "./Modules/Authentication/Register/Components/RegisterComponent";
import { LeadAddComponent } from "./Modules/Components/LeadAddComponent";
import { PhotoUploadComponent } from "./Modules/Trips/Components/PhotoUploadComponent";
import { ProjectDetailComponent } from "./Modules/Project/Components/ProjectDetailComponent";
import { ProjectListComponent } from "./Modules/Project/Components/ProjectListComponent";
import { SettingsComponent } from "./Modules/Settings/Components/SettingsComponent";
import { TripDetailComponent } from "./Modules/Trips/Components/TripDetailComponent";

export const AppRouting: React.FC = () => {
  return (
    <Router>
      <Routes>
        <Route path="/login" element={<LoginComponent />}></Route>
        <Route path="/register" element={<RegisterComponent />}></Route>
        <Route
          path="/reset-password"
          element={<ResetPasswordComponent />}
        ></Route>
        <Route
          path="/confirm-email"
          element={<ConfirmEmailComponent />}
        ></Route>
        <Route element={<ProtectedRoutesComponent />}>
          <Route path="/projects" element={<ProjectListComponent />}></Route>{" "}
          <Route
            path="/projects/:projectId"
            element={<ProjectDetailComponent />}
          ></Route>
          <Route
            path="/projects/:projectId"
            element={<ProjectDetailComponent />}
          ></Route>
          <Route
            path="/projects/:projectId/trip/:tripId"
            element={<TripDetailComponent />}
          ></Route>
          <Route
            path="/projects/:projectId/trip/:tripId/uploadPhotos"
            element={<PhotoUploadComponent />}
          ></Route>
          <Route
            path="/projects/:projectId/trip/:tripId/addLeads"
            element={<LeadAddComponent />}
          ></Route>
          <Route path="/settings" element={<SettingsComponent />}></Route>
          <Route path="*" element={<Navigate to="/projects" replace />} />{" "}
        </Route>
      </Routes>
    </Router>
  );
};

export interface HasRoute {
  route: string;
}
