export function configurationSaveErrorMessage(error: unknown): string {
  const status = (error as Error & { status?: number })?.status;
  return status === 409
    ? 'Another user changed this shelf. Reload before saving again.'
    : String(error);
}
