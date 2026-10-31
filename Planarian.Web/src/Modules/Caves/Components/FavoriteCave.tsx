import React, { useEffect, useState } from "react";
import { StarFilled, StarOutlined } from "@ant-design/icons";
import { message } from "antd";
import { PlanarianButton } from "../../../Shared/Components/Buttons/PlanarianButtton";
import { CaveService } from "../Service/CaveService";

interface FavoriteCaveProps {
  caveId?: string;
  initialIsFavorite?: boolean;
  onlyShowWhenFavorite?: boolean;
  disabled?: boolean;
}

const FavoriteCave: React.FC<FavoriteCaveProps> = ({
  caveId,
  initialIsFavorite,
  onlyShowWhenFavorite = false,
  disabled = false,
}) => {
  const [isFavorite, setIsFavorite] = useState<boolean>(
    initialIsFavorite || false
  );
  const [isLoading, setIsLoading] = useState<boolean>(
    !initialIsFavorite || !caveId
  );
  const [isUpdating, setIsUpdating] = useState<boolean>(false);

  useEffect(() => {
    if (initialIsFavorite !== undefined || !caveId) {
      setIsLoading(false);
      return;
    }

    const fetchFavoriteStatus = async () => {
      try {
        setIsLoading(true);
        const favoriteVm = await CaveService.GetFavoriteCaveVm(caveId);
        setIsFavorite(!!favoriteVm);
      } catch (error) {
        console.error("Failed to fetch favorite status:", error);
        message.error("Failed to load favorite status");
      } finally {
        setIsLoading(false);
      }
    };

    fetchFavoriteStatus();
  }, [caveId, initialIsFavorite]);

  useEffect(() => {
    if (!caveId) {
      setIsLoading(true);
    }
  }, [caveId]);

  const handleFavoriteToggle = async () => {
    if (disabled || !caveId) {
      return;
    }

    try {
      setIsUpdating(true);
      if (isFavorite) {
        await CaveService.UnfavoriteCave(caveId);
        setIsFavorite(false);
        message.success("Removed from favorites");
      } else {
        await CaveService.FavoriteCave(caveId);
        setIsFavorite(true);
        message.success("Added to favorites");
      }
    } catch (error) {
      message.error("Failed to update favorite status");
    } finally {
      setIsUpdating(false);
    }
  };

  if (onlyShowWhenFavorite && !isFavorite && !isLoading && !isUpdating) {
    return null;
  }

  const favoriteIcon = isFavorite ? (
    <StarFilled style={{ color: "gold" }} />
  ) : (
    <StarOutlined />
  );

  if (!caveId) {
    return (
      <PlanarianButton
        icon={favoriteIcon}
        aria-label="Loading favorite status"
        loading={true}
        disabled={true}
      />
    );
  }

  if (disabled) {
    return favoriteIcon;
  }

  return (
    <PlanarianButton
      icon={favoriteIcon}
      onClick={handleFavoriteToggle}
      loading={isLoading || isUpdating}
      disabled={isLoading || isUpdating}
    >
      Favorite
    </PlanarianButton>
  );
};

export default FavoriteCave;
