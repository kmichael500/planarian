import { render, screen } from "@testing-library/react";
import React, { useContext } from "react";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { AppContext } from "../../../Configuration/Context/AppContext";
import { ClientRoutes } from "../../../Configuration/Routing/ClientRoutes.generated";
import { ProtectedRoutesComponent } from "./ProtectedRoutesComponent";

it("redirects an authenticated user without an active account away from account routes", async () => {
  const Wrapper = ({ children }: { children: React.ReactNode }) => {
    const defaults = useContext(AppContext);
    return (
      <AppContext.Provider
        value={{
          ...defaults,
          isAuthenticated: true,
          currentAccountId: null,
          currentUser: {
            id: "user123456",
            fullName: "Test User",
            currentAccountId: null,
          },
        }}
      >
        {children}
      </AppContext.Provider>
    );
  };

  render(
    <MemoryRouter initialEntries={["/caves"]}>
      <Wrapper>
        <Routes>
          <Route element={<ProtectedRoutesComponent requiresAccount />}>
            <Route path="/caves" element={<div>Caves</div>} />
          </Route>
          <Route
            path={ClientRoutes.invitationList.path}
            element={<div>Invitations</div>}
          />
        </Routes>
      </Wrapper>
    </MemoryRouter>
  );

  expect(await screen.findByText("Invitations")).toBeInTheDocument();
  expect(screen.queryByText("Caves")).not.toBeInTheDocument();
});
