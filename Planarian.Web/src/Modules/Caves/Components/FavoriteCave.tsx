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
    // Skip fetching if initialIsFavorite is provided or if caveId is missing
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

  // Update loading state when caveId changes
  useEffect(() => {
    if (!caveId) {
      setIsLoading(true);
    }
  }, [caveId]);

  const handleFavoriteToggle = async (e: React.MouseEvent) => {
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

  // Don't render anything if onlyShowWhenFavorite is true and the item is not favorited
  if (onlyShowWhenFavorite && !isFavorite && !isLoading && !isUpdating) {
    return null;
  }

  // Basic icon without any custom styling
  const favoriteIcon = isFavorite ? (
    <StarFilled style={{ color: "gold" }} />
  ) : (
    <StarOutlined />
  );

  // Show loading indicator if caveId is missing
  if (!caveId) {
    return (
      <PlanarianButton icon={favoriteIcon} loading={true} disabled={true} />
    );
  }

  // If disabled, just render the icon without a button wrapper
  if (disabled) {
    return favoriteIcon;
  }

  // Otherwise render the interactive button
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
