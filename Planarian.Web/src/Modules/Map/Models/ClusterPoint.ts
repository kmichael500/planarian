export interface ClusterPoint {
  name: string;
  latitude: number;
  longitude: number;
  elevation: number;
}

export interface Cluster {
  clusterPoints: ClusterPoint[];
}
