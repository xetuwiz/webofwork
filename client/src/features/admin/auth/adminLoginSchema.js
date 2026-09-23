export function validateAdminLogin(values) {
  const errors = {};
  if (!values.email || !/^\S+@\S+\.\S+$/.test(values.email)) errors.email = "Enter a valid email address.";
  if (!values.password || values.password.length < 6) errors.password = "Password must be at least 6 characters.";
  return errors;
}