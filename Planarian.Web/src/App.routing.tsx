import {
  BrowserRouter as Router,
  Routes,
  Route,
  Link,
  Navigate,
} from "react-router-dom";
import { ConfirmEmailComponent } from "./Modules/Authentication/Components/confirm.email.component";
import { LoginComponent } from "./Modules/Authentication/Components/login.component";
import { ProtectedRoutes } from "./Modules/Authentication/Components/protected.routes.component";
import { ResetPasswordComponent } from "./Modules/Authentication/Components/reset.password.component";
import { RegisterComponent } from "./Modules/Authentication/Register/Components/register.component";
import { LeadAddComponent } from "./Modules/Components/lead.add.component";
import { PhotoUploadComponent } from "./Modules/Trips/Components/PhotoUploadComponent";
import { ProjectDetailComponent } from "./Modules/Project/Components/project.detail.component";
import { ProjectListComponent } from "./Modules/Project/Components/project.list.component";
import { SettingsComponent } from "./Modules/Settings/Components/settings.component";
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
        <Route element={<ProtectedRoutes />}>
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
