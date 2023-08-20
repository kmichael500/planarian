export class CacheService {
  private static cache: Map<string, any> = new Map<string, any>();
  private static defaultTTL: number = 3600;

  static set<T>(key: string, value: T, ttl: number = this.defaultTTL): void {
    this.cache.set(key, value);
    if (ttl > 0) {
      setTimeout(() => this.delete(key), ttl * 1000);
    }
  }

  static get<T>(key: string): T | undefined {
    return this.cache.get(key);
  }

  static has(key: string): boolean {
    return this.cache.has(key);
  }

  static delete(key: string): void {
    this.cache.delete(key);
  }

  static clear(): void {
    this.cache.clear();
  }

  static size(): number {
    return this.cache.size;
  }

  static setDefaultTTL(defaultTTL: number): void {
    this.defaultTTL = defaultTTL;
  }
}
