import React from "react";
import { Link } from "react-router-dom";
import { AddButtonComponent } from "../../../Shared/Components/Buttons/AddButtonComponent";

const CaveCreateButtonComponent: React.FC = () => {
  return (
    <Link to={`/caves/add`}>
      <AddButtonComponent
        alwaysShowChildren
        className="caves-page__header-add-button"
        type="primary"
      />
    </Link>
  );
};

export { CaveCreateButtonComponent };
