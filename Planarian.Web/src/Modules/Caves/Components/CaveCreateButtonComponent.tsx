import React from "react";
import { Link } from "react-router-dom";
import { AddButtonComponent } from "../../../Shared/Components/Buttons/AddButtonComponent";

interface CaveCreateButtonProps {}

const CaveCreateButtonComponent: React.FC<CaveCreateButtonProps> = (
  props: CaveCreateButtonProps
) => {
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
