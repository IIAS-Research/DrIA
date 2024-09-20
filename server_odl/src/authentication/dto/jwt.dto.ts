import { User } from '../../users/entities/user.entity';

export class JwtUserDto {
  email: User['email'];
  sub: User['id'];
  firstName: User['firstName'];
  lastName: User['lastName'];
}
