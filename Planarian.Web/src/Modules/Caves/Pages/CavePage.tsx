import { useContext, useEffect, useState } from "react";
import { CaveVm } from "../Models/CaveVm";
import { Link, useParams } from "react-router-dom";
import { AppContext } from "../../../Configuration/Context/AppContext";
import { BackButtonComponent } from "../../../Shared/Components/Buttons/BackButtonComponent";
import { NotFoundException } from "../../../Shared/Exceptions/NotFoundException";
import { CaveService } from "../Service/CaveService";
import { CaveComponent } from "../Components/CaveComponent";
import { PlanarianButton } from "../../../Shared/Components/Buttons/PlanarianButtton";
import { EditOutlined } from "@ant-design/icons";
import { isNullOrWhiteSpace } from "../../../Shared/Helpers/StringHelpers";

const CavePage = () => {
  const [cave, setCave] = useState<CaveVm>();

  const [isLoading, setIsLoading] = useState<boolean>(true);

  const { setHeaderTitle, setHeaderButtons } = useContext(AppContext);
  const { caveId } = useParams();

  useEffect(() => {
    setHeaderButtons([
      <Link to={`/caves/${caveId}/edit`}>
        <PlanarianButton icon={<EditOutlined />}>Edit</PlanarianButton>{" "}
      </Link>,
      <BackButtonComponent to={"./.."} />,
    ]);
  }, [cave]);

  if (caveId === undefined) {
    throw new NotFoundException();
  }

  useEffect(() => {
    if (!isNullOrWhiteSpace(cave?.name)) {
      setHeaderTitle([`${cave?.displayId} ${cave?.name ?? ""}`]);
    }
  }, [cave]);
  useEffect(() => {
    const getCave = async () => {
      const caveResponse = await CaveService.GetCave(caveId);
      setCave(caveResponse);
      setIsLoading(false);
    };
    getCave();
  }, []);

  return (
    <>
      <CaveComponent cave={cave} isLoading={isLoading} />
    </>
  );
};

export { CavePage };
