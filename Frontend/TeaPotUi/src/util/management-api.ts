import axios from "axios";

export const changePassword = async (email: string, newPassword: string) => {
  const { status } = await axios.patch(
    `${import.meta.env.VITE_API_BASE_URL}/api/users/management/change-password`,
    { email: email, password: newPassword },
  );

  return status >= 200 && status < 300;
};
