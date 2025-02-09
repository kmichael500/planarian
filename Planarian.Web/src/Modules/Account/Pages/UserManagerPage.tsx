import React, { useContext, useEffect } from "react";
import { Row } from "antd";
import { AppContext } from "../../../Configuration/Context/AppContext";
import { AccountSettingsComponent } from "../Components/AccountSettingsComponent";
import { UserManagerComponent } from "../Components/UserManagerComponent";


const UserManagerPage: React.FC = () => {
    const { setHeaderTitle, setHeaderButtons } = useContext(AppContext);

    useEffect(() => {
        setHeaderButtons([]);
        setHeaderTitle(["User Manager"]);
    }, []);

    return (
        <>
            <UserManagerComponent />
        </>
    );
};

export { UserManagerPage };
